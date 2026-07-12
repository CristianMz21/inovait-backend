#!/usr/bin/env bash
# deploy-local.sh
#
# Brings up the whole local Inovait stack for evaluators: SQL Server
# (Docker), the Inovait API, and the Angular frontend built in production
# configuration talking real HTTP to the API (no mocks). Also tears the
# whole stack down cleanly.
#
# Usage:
#   ./scripts/deploy-local.sh [options]
#   ./scripts/deploy-local.sh --down
#   ./scripts/deploy-local.sh --check-only
#
# Options:
#   --api-port <port>         API port (default: 5000)
#   --sql-port <port>         Host port for the SQL Server container (default: 1433)
#   --frontend-path <path>    Path to the inovait-frontend checkout
#                              (default: ../inovait-frontend, relative to this repo)
#   --sa-password <password>  SQL Server SA password (default: generated at
#                              runtime, printed once, never written to disk)
#   --skip-frontend            Skip building/serving the Angular frontend
#   --no-demo-data             Skip seeding fictitious local-evaluation demo data
#   --down                     Tear down a previously started stack
#   --check-only                Run prerequisite checks only, then exit
#   -h, --help                  Show this help and exit
#
# The generated (or supplied) SA password only ever lives in this process's
# environment and the environment of the child processes it starts -- it is
# NEVER written to a file. `--down` does not need it: a dummy value is used
# only to satisfy docker compose variable interpolation while tearing down.
set -euo pipefail

ORIGINAL_PWD="$PWD"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

STATE_DIR="$REPO_ROOT/.local-stack"
COMPOSE_SERVICE="sql-server"
SQLCMD="/opt/mssql-tools18/bin/sqlcmd"

API_PORT=5000
SQL_PORT=1433
FRONTEND_PATH="$REPO_ROOT/../inovait-frontend"
SA_PASSWORD=""
SA_PASSWORD_GENERATED=0
SKIP_FRONTEND=0
NO_DEMO_DATA=0
DOWN=0
CHECK_ONLY=0

# ---------------------------------------------------------------------------
# Small helpers
# ---------------------------------------------------------------------------

usage() {
  cat <<'EOF'
Usage: deploy-local.sh [options]

  --api-port <port>         API port (default: 5000)
  --sql-port <port>         Host port for the SQL Server container (default: 1433)
  --frontend-path <path>    Path to the inovait-frontend checkout
                             (default: ../inovait-frontend, relative to this repo)
  --sa-password <password>  SQL Server SA password (default: generated at runtime)
  --skip-frontend            Skip building/serving the Angular frontend
  --no-demo-data             Skip seeding fictitious local-evaluation demo data
  --down                     Tear down a previously started stack
  --check-only                Run prerequisite checks only, then exit
  -h, --help                  Show this help and exit
EOF
}

fail() {
  echo "ERROR: $*" >&2
  exit 1
}

require_cmd() {
  local cmd="$1" hint="$2"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    fail "Required command '$cmd' not found. Install $hint and retry."
  fi
}

port_in_use() {
  local port="$1"
  if (exec 3<>"/dev/tcp/127.0.0.1/$port") 2>/dev/null; then
    exec 3<&- 3>&-
    return 0
  fi
  return 1
}

check_port_free() {
  local port="$1" label="$2" suggestion="$3"
  if port_in_use "$port"; then
    fail "Port $port ($label) is already in use. $suggestion"
  fi
}

# ---------------------------------------------------------------------------
# Prerequisite checks
# ---------------------------------------------------------------------------

check_prereqs() {
  echo "Checking prerequisites..."

  require_cmd docker "Docker (https://docs.docker.com/get-docker/)"
  if ! docker info >/dev/null 2>&1; then
    fail "Docker daemon is not running (or not reachable). Start Docker and retry."
  fi
  if ! docker compose version >/dev/null 2>&1; then
    fail "'docker compose' (v2 plugin) is not available. Install/update Docker to a version that ships the compose plugin."
  fi

  require_cmd dotnet ".NET SDK (https://dotnet.microsoft.com/download)"
  local required_major have_major installed_sdks
  required_major="$(grep -oE '"version"[[:space:]]*:[[:space:]]*"[0-9]+' global.json | grep -oE '[0-9]+$' || true)"
  [[ -n "$required_major" ]] || fail "Could not parse the required SDK major version from global.json."
  installed_sdks="$(dotnet --list-sdks 2>/dev/null || true)"
  have_major="$(printf '%s\n' "$installed_sdks" | awk '{print $1}' | cut -d. -f1 | grep -x "$required_major" || true)"
  if [[ -z "$have_major" ]]; then
    fail "No installed .NET SDK matches major version ${required_major}.x (required by global.json). Installed SDKs: $(printf '%s' "$installed_sdks" | tr '\n' ';')"
  fi

  require_cmd curl "curl (needed to poll API/frontend readiness)"

  if [[ "$SKIP_FRONTEND" -eq 0 ]]; then
    require_cmd node "Node.js (https://nodejs.org/)"
    require_cmd npm "npm (bundled with Node.js)"
    local node_major
    node_major="$(node --version 2>/dev/null | sed -E 's/^v([0-9]+).*/\1/')"
    if [[ "$node_major" != "24" ]]; then
      echo "WARNING: Node.js v24.x is recommended; found $(node --version 2>/dev/null)." >&2
    fi
  fi

  check_port_free "$API_PORT" "API" "Choose a different port with --api-port."
  check_port_free "$SQL_PORT" "SQL Server" "Choose a different port with --sql-port."
  if [[ "$SKIP_FRONTEND" -eq 0 ]]; then
    check_port_free 4200 "frontend" "Stop whatever is using it, or re-run with --skip-frontend."
  fi

  echo "All prerequisite checks passed."
}

# ---------------------------------------------------------------------------
# SA password (generated in-memory only; never written to disk)
# ---------------------------------------------------------------------------

generate_password() {
  local charset='ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789'
  local symbols='!@#%^*_+='
  local body symbol_part
  body="$(LC_ALL=C tr -dc "$charset" < /dev/urandom | head -c 20)"
  symbol_part="$(LC_ALL=C tr -dc "$symbols" < /dev/urandom | head -c 2)"
  # Guarantees upper/lower/digit/symbol characters so SQL Server's password
  # complexity policy (>= 3 of those 4 categories) is always satisfied.
  printf '%sAa1%s' "$body" "$symbol_part"
}

# ---------------------------------------------------------------------------
# Readiness polling
# ---------------------------------------------------------------------------

wait_for_http() {
  local url="$1" timeout="$2" label="$3" logfile="$4"
  local waited=0 interval=2
  printf 'Waiting for %s to respond at %s' "$label" "$url"
  while true; do
    if curl -fsS -o /dev/null -m 3 "$url" 2>/dev/null; then
      echo " ready."
      return 0
    fi
    if [[ "$waited" -ge "$timeout" ]]; then
      echo
      echo "ERROR: $label did not become ready within ${timeout}s ($url)." >&2
      if [[ -f "$logfile" ]]; then
        echo "---- last 40 lines of $logfile ----" >&2
        tail -n 40 "$logfile" >&2
      fi
      return 1
    fi
    printf '.'
    sleep "$interval"
    waited=$((waited + interval))
  done
}

# ---------------------------------------------------------------------------
# Stack steps
# ---------------------------------------------------------------------------

start_sql() {
  echo "Starting SQL Server container (this can take up to ~30s for the healthcheck to pass)..."
  # Exported for the whole script lifetime: docker compose re-interpolates the
  # compose file on EVERY invocation (up/cp/exec/down), not just `up`. The env
  # dies with this process and is never written to disk.
  export MSSQL_SA_PASSWORD="$SA_PASSWORD"
  export INOVAIT_SQL_PORT="$SQL_PORT"
  if ! docker compose up -d --wait; then
    fail "SQL Server container failed to become healthy. Inspect it with: docker compose logs ${COMPOSE_SERVICE}"
  fi
  echo "SQL Server is up on port ${SQL_PORT}."
}

apply_database() {
  echo "Preparing database (Inovait) and applying database/setup.sql..."
  docker compose cp database/setup.sql "${COMPOSE_SERVICE}:/tmp/setup.sql"
  # KNOWN GOTCHA: `docker compose cp` preserves the host file's uid/mode
  # (typically 0640, owned by a uid the container's mssql user cannot read).
  # Force it world-readable inside the container before sqlcmd reads it.
  docker compose exec -T -u root "$COMPOSE_SERVICE" chmod 0444 /tmp/setup.sql
  docker compose exec -T "$COMPOSE_SERVICE" "$SQLCMD" -C -S localhost -U sa -P "$SA_PASSWORD" -b \
    -Q "IF DB_ID('Inovait') IS NULL CREATE DATABASE Inovait"
  docker compose exec -T "$COMPOSE_SERVICE" "$SQLCMD" -C -S localhost -U sa -P "$SA_PASSWORD" -b \
    -d Inovait -i /tmp/setup.sql
  echo "Database ready (setup.sql is idempotent -- safe on re-run)."
}

seed_demo_data() {
  if [[ "$NO_DEMO_DATA" -eq 1 ]]; then
    echo "Skipping demo data (--no-demo-data)."
    return 0
  fi

  # WHY THIS EXISTS: the canonical production seed only contains DocumentType
  # 'CC' and no class groups/teachers, so the frontend enrollment form (which
  # offers DNI/PAS/CE per the contract examples) always gets 404 and no
  # walkthrough is possible without extra data. This step seeds FICTITIOUS
  # LOCAL-EVALUATION data only -- it is never part of the production seed.
  echo "Seeding fictitious local-evaluation demo data (skip with --no-demo-data)..."

  local demo_sql="$STATE_DIR/demo-data.sql"
  # Pure ASCII on purpose: accented characters are emitted via NCHAR() so the
  # file survives every encoding layer (shell, docker cp, sqlcmd codepage).
  cat > "$demo_sql" <<'SQL'
-- Fictitious LOCAL-EVALUATION demo data. NEVER part of the production seed.
-- Single idempotent batch: every INSERT is guarded by IF NOT EXISTS on its
-- natural key, so re-running is a safe no-op.
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRY
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRANSACTION;

    -- Document types the frontend enrollment form offers (DNI/PAS/CE).
    -- IsActive is NOT NULL with no default, so it is set explicitly.
    IF NOT EXISTS (SELECT 1 FROM [catalog].[DocumentType] WHERE [Code] = 'DNI')
    BEGIN
        INSERT [catalog].[DocumentType] ([Code],[Name],[IsActive])
            VALUES ('DNI', N'Documento Nacional de Identidad', 1);
        PRINT 'seeded : DocumentType DNI';
    END
    ELSE PRINT 'skipped: DocumentType DNI (exists)';

    IF NOT EXISTS (SELECT 1 FROM [catalog].[DocumentType] WHERE [Code] = 'PAS')
    BEGIN
        INSERT [catalog].[DocumentType] ([Code],[Name],[IsActive])
            VALUES ('PAS', N'Pasaporte', 1);
        PRINT 'seeded : DocumentType PAS';
    END
    ELSE PRINT 'skipped: DocumentType PAS (exists)';

    IF NOT EXISTS (SELECT 1 FROM [catalog].[DocumentType] WHERE [Code] = 'CE')
    BEGIN
        -- NCHAR(233)=e-acute, NCHAR(237)=i-acute: "Cedula de Extranjeria"
        -- with accents, kept ASCII-safe in this file.
        INSERT [catalog].[DocumentType] ([Code],[Name],[IsActive])
            VALUES ('CE', N'C' + NCHAR(233) + N'dula de Extranjer' + NCHAR(237) + N'a', 1);
        PRINT 'seeded : DocumentType CE';
    END
    ELSE PRINT 'skipped: DocumentType CE (exists)';

    -- Second school (natural key: Code).
    IF NOT EXISTS (SELECT 1 FROM [catalog].[School] WHERE [Code] = 'SCH-002')
    BEGIN
        INSERT [catalog].[School] ([Code],[Name],[Sector])
            VALUES ('SCH-002', N'South Learning Center', 'Public');
        PRINT 'seeded : School SCH-002';
    END
    ELSE PRINT 'skipped: School SCH-002 (exists)';

    -- Class group in the canonical school/year/grade (natural key: context).
    IF NOT EXISTS (SELECT 1 FROM [academic].[ClassGroup]
                   WHERE [SchoolId] = 1 AND [AcademicYearId] = 1 AND [GradeId] = 1 AND [Code] = 'CG-01')
    BEGIN
        INSERT [academic].[ClassGroup] ([SchoolId],[AcademicYearId],[GradeId],[Code])
            VALUES (1, 1, 1, 'CG-01');
        PRINT 'seeded : ClassGroup CG-01';
    END
    ELSE PRINT 'skipped: ClassGroup CG-01 (exists)';

    DECLARE @classGroupId int = (SELECT [Id] FROM [academic].[ClassGroup]
        WHERE [SchoolId] = 1 AND [AcademicYearId] = 1 AND [GradeId] = 1 AND [Code] = 'CG-01');

    -- Demo teacher person (natural key: DocumentTypeId + DocumentNumber).
    IF NOT EXISTS (SELECT 1 FROM [people].[Person]
                   WHERE [DocumentTypeId] = 1 AND [DocumentNumber] = N'TCH-0001')
    BEGIN
        INSERT [people].[Person] ([DocumentTypeId],[DocumentNumber],[FirstNames],[LastNames],[BirthDate])
            VALUES (1, N'TCH-0001', N'Ana', N'Gomez', '1985-04-12');
        PRINT 'seeded : Person TCH-0001 (Ana Gomez)';
    END
    ELSE PRINT 'skipped: Person TCH-0001 (exists)';

    DECLARE @teacherPersonId int = (SELECT [Id] FROM [people].[Person]
        WHERE [DocumentTypeId] = 1 AND [DocumentNumber] = N'TCH-0001');

    IF NOT EXISTS (SELECT 1 FROM [people].[Teacher] WHERE [PersonId] = @teacherPersonId)
    BEGIN
        INSERT [people].[Teacher] ([PersonId]) VALUES (@teacherPersonId);
        PRINT 'seeded : Teacher role for TCH-0001';
    END
    ELSE PRINT 'skipped: Teacher role for TCH-0001 (exists)';

    -- Subject (natural key: Code).
    IF NOT EXISTS (SELECT 1 FROM [catalog].[Subject] WHERE [Code] = 'MATH')
    BEGIN
        INSERT [catalog].[Subject] ([Code],[Name]) VALUES ('MATH', N'Mathematics');
        PRINT 'seeded : Subject MATH';
    END
    ELSE PRINT 'skipped: Subject MATH (exists)';

    DECLARE @subjectId int = (SELECT [Id] FROM [catalog].[Subject] WHERE [Code] = 'MATH');

    -- Confirmed contract for the demo teacher at the canonical school
    -- (natural key: teacher + school + start date).
    IF NOT EXISTS (SELECT 1 FROM [staff].[TeacherContract]
                   WHERE [TeacherPersonId] = @teacherPersonId AND [SchoolId] = 1 AND [StartDate] = '2026-01-15')
    BEGIN
        INSERT [staff].[TeacherContract] ([TeacherPersonId],[SchoolId],[StartDate],[EndDate],[Status])
            VALUES (@teacherPersonId, 1, '2026-01-15', NULL, 'Confirmed');
        PRINT 'seeded : TeacherContract (TCH-0001 @ SchoolId 1, 2026-01-15)';
    END
    ELSE PRINT 'skipped: TeacherContract (exists)';

    DECLARE @contractId int = (SELECT [Id] FROM [staff].[TeacherContract]
        WHERE [TeacherPersonId] = @teacherPersonId AND [SchoolId] = 1 AND [StartDate] = '2026-01-15');

    -- Teaching assignment (natural key: contract + class group + subject).
    IF NOT EXISTS (SELECT 1 FROM [academic].[TeachingAssignment]
                   WHERE [TeacherContractId] = @contractId AND [ClassGroupId] = @classGroupId AND [SubjectId] = @subjectId)
    BEGIN
        INSERT [academic].[TeachingAssignment] ([TeacherContractId],[ClassGroupId],[SubjectId],[StartDate],[EndDate])
            VALUES (@contractId, @classGroupId, @subjectId, '2026-02-01', NULL);
        PRINT 'seeded : TeachingAssignment (MATH @ CG-01)';
    END
    ELSE PRINT 'skipped: TeachingAssignment (exists)';

    DECLARE @assignmentId int = (SELECT [Id] FROM [academic].[TeachingAssignment]
        WHERE [TeacherContractId] = @contractId AND [ClassGroupId] = @classGroupId AND [SubjectId] = @subjectId);

    -- Class schedule on weekdays 1 (Monday) and 3 (Wednesday).
    IF NOT EXISTS (SELECT 1 FROM [academic].[ClassSchedule]
                   WHERE [TeachingAssignmentId] = @assignmentId AND [Weekday] = 1)
    BEGIN
        INSERT [academic].[ClassSchedule] ([TeachingAssignmentId],[Weekday]) VALUES (@assignmentId, 1);
        PRINT 'seeded : ClassSchedule weekday 1';
    END
    ELSE PRINT 'skipped: ClassSchedule weekday 1 (exists)';

    IF NOT EXISTS (SELECT 1 FROM [academic].[ClassSchedule]
                   WHERE [TeachingAssignmentId] = @assignmentId AND [Weekday] = 3)
    BEGIN
        INSERT [academic].[ClassSchedule] ([TeachingAssignmentId],[Weekday]) VALUES (@assignmentId, 3);
        PRINT 'seeded : ClassSchedule weekday 3';
    END
    ELSE PRINT 'skipped: ClassSchedule weekday 3 (exists)';

    COMMIT TRANSACTION;
    PRINT 'Demo data batch complete.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
SQL

  docker compose cp "$demo_sql" "${COMPOSE_SERVICE}:/tmp/demo-data.sql"
  # Same uid/mode gotcha as setup.sql: make it readable for the mssql user.
  docker compose exec -T -u root "$COMPOSE_SERVICE" chmod 0444 /tmp/demo-data.sql
  docker compose exec -T "$COMPOSE_SERVICE" "$SQLCMD" -C -S localhost -U sa -P "$SA_PASSWORD" -b \
    -d Inovait -i /tmp/demo-data.sql
  echo "Demo data ready (idempotent -- per-row seeded/skipped summary above)."
}

start_api() {
  echo "Building API (Release)..."
  dotnet build src/Inovait.Api/Inovait.Api.csproj --configuration Release

  echo "Starting API on http://localhost:${API_PORT} ..."
  (
    cd "$REPO_ROOT"
    # KNOWN GOTCHA: `dotnet run` loads launchSettings.json and IGNORES
    # ASPNETCORE_URLS unless --no-launch-profile is passed -- that flag is
    # mandatory here, otherwise the API binds to the launchSettings ports.
    # TrustServerCertificate=True below targets ONLY this container's
    # self-signed dev certificate; this connection string is process-env
    # only and must never be committed to any config file.
    ASPNETCORE_URLS="http://localhost:${API_PORT}" \
    ConnectionStrings__InovaitDatabase="Server=localhost,${SQL_PORT};Database=Inovait;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True" \
    nohup dotnet run --project src/Inovait.Api --configuration Release --no-build --no-launch-profile \
      > "$STATE_DIR/api.log" 2>&1 &
    echo $! > "$STATE_DIR/api.pid"
  )

  if ! wait_for_http "http://localhost:${API_PORT}/health/ready" 60 "API" "$STATE_DIR/api.log"; then
    exit 1
  fi
}

start_frontend() {
  if [[ "$SKIP_FRONTEND" -eq 1 ]]; then
    echo "Skipping frontend (--skip-frontend)."
    return 0
  fi

  if [[ ! -d "$FRONTEND_PATH" ]]; then
    fail "Frontend path not found: $FRONTEND_PATH. Pass --frontend-path <path> or clone inovait-frontend next to this repo."
  fi

  if [[ ! -d "$FRONTEND_PATH/node_modules" ]]; then
    echo "First run: installing frontend dependencies (npm ci) -- this can take a few minutes..."
    (cd "$FRONTEND_PATH" && npm ci)
  fi

  echo "Starting frontend on http://localhost:4200 (production configuration) ..."
  (
    cd "$FRONTEND_PATH"
    nohup npx ng serve --configuration production --port 4200 --host 127.0.0.1 \
      > "$STATE_DIR/frontend.log" 2>&1 &
    echo $! > "$STATE_DIR/frontend.pid"
  )

  # The app must be opened via http://localhost:4200 (NOT 127.0.0.1:4200):
  # the API's CORS allowlist only contains the localhost origin, so a
  # 127.0.0.1 origin gets CORS-blocked. ng serve still binds 127.0.0.1 above.
  # The Angular dev-server production build can take 60-120s; be generous.
  if ! wait_for_http "http://localhost:4200" 180 "frontend" "$STATE_DIR/frontend.log"; then
    exit 1
  fi
}

stop_pid_file() {
  local pid_file="$1" label="$2"
  if [[ -f "$pid_file" ]]; then
    local pid
    pid="$(cat "$pid_file" 2>/dev/null || true)"
    if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
      echo "Stopping $label (pid $pid)..."
      kill "$pid" 2>/dev/null || true
      local i
      for i in 1 2 3 4 5; do
        kill -0 "$pid" 2>/dev/null || break
        sleep 1
      done
      if kill -0 "$pid" 2>/dev/null; then
        kill -9 "$pid" 2>/dev/null || true
      fi
    fi
    rm -f "$pid_file"
  fi
}

down_stack() {
  echo "Tearing down local stack..."
  stop_pid_file "$STATE_DIR/api.pid" "API"
  stop_pid_file "$STATE_DIR/frontend.pid" "frontend"

  require_cmd docker "Docker (https://docs.docker.com/get-docker/)"

  # docker compose requires MSSQL_SA_PASSWORD for variable interpolation even
  # to tear down -- a dummy value is fine here, it is never used for auth.
  MSSQL_SA_PASSWORD="${MSSQL_SA_PASSWORD:-teardown-only-unused}" docker compose down -v

  echo "Stack is down. (.local-stack/*.log kept for inspection; safe to delete.)"
}

# ---------------------------------------------------------------------------
# Argument parsing
# ---------------------------------------------------------------------------

while [[ $# -gt 0 ]]; do
  case "$1" in
    --api-port)
      API_PORT="$2"; shift 2 ;;
    --sql-port)
      SQL_PORT="$2"; shift 2 ;;
    --frontend-path)
      FRONTEND_PATH="$2"; shift 2 ;;
    --sa-password)
      SA_PASSWORD="$2"; shift 2 ;;
    --skip-frontend)
      SKIP_FRONTEND=1; shift ;;
    --no-demo-data)
      NO_DEMO_DATA=1; shift ;;
    --down)
      DOWN=1; shift ;;
    --check-only)
      CHECK_ONLY=1; shift ;;
    -h|--help)
      usage; exit 0 ;;
    *)
      fail "Unknown option: $1 (use --help for usage)" ;;
  esac
done

if [[ "$FRONTEND_PATH" != /* ]]; then
  FRONTEND_PATH="$ORIGINAL_PWD/$FRONTEND_PATH"
fi

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

mkdir -p "$STATE_DIR"

if [[ "$DOWN" -eq 1 ]]; then
  down_stack
  exit 0
fi

if [[ "$CHECK_ONLY" -eq 1 ]]; then
  check_prereqs
  exit 0
fi

check_prereqs

if [[ -z "$SA_PASSWORD" ]]; then
  SA_PASSWORD="$(generate_password)"
  SA_PASSWORD_GENERATED=1
fi

start_sql
apply_database
seed_demo_data
start_api
start_frontend

echo
echo "================================================================"
echo " Inovait local stack is up"
echo "================================================================"
if [[ "$SA_PASSWORD_GENERATED" -eq 1 ]]; then
  echo "Generated SQL Server SA password (shown once, never written to disk):"
  echo "  $SA_PASSWORD"
  echo "You do not need it to tear the stack down."
  echo
fi
if [[ "$SKIP_FRONTEND" -eq 1 ]]; then
  echo "Frontend:   skipped (--skip-frontend)"
else
  # localhost, not 127.0.0.1: the API's CORS allowlist only has this origin.
  echo "Frontend:   http://localhost:4200"
fi
if [[ "$NO_DEMO_DATA" -eq 1 ]]; then
  echo "Demo data:  skipped (--no-demo-data)"
else
  echo "Demo data:  seeded (fictitious local-evaluation data only)"
fi
echo "API:        http://localhost:${API_PORT}"
echo "Health:     http://localhost:${API_PORT}/health/ready"
echo "Logs:       $STATE_DIR/api.log, $STATE_DIR/frontend.log"
echo "Teardown:   $REPO_ROOT/scripts/deploy-local.sh --down"
echo "================================================================"
