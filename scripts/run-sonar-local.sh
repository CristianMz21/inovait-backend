#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

if [[ -z "${SONAR_TOKEN:-}" ]]; then
  echo "ERROR: SONAR_TOKEN is required." >&2
  exit 1
fi

SONAR_HOST_URL="${SONAR_HOST_URL:-http://localhost:9000}"
if [[ ! "$SONAR_HOST_URL" =~ ^https:// ]] &&
  [[ ! "$SONAR_HOST_URL" =~ ^http://(localhost|127\.0\.0\.1|\[::1\])(:[0-9]+)?/?$ ]]; then
  echo "ERROR: SONAR_HOST_URL must use HTTPS unless it targets the local machine." >&2
  exit 1
fi

SONAR_TOKEN_VALUE="$SONAR_TOKEN"
export -n SONAR_TOKEN_VALUE
unset SONAR_TOKEN
SONAR_PROJECT_KEY="${SONAR_PROJECT_KEY:-inovait-backend}"
SONAR_PROJECT_NAME="${SONAR_PROJECT_NAME:-Inovait Backend}"
CONFIGURATION="${CONFIGURATION:-Release}"
SOLUTION="Inovait.slnx"
RESULTS_DIR="$REPO_ROOT/TestResults/Sonar/$(date -u +%Y%m%dT%H%M%SZ)"
SONAR_WORK_DIR="$REPO_ROOT/.sonarqube"

TEST_PROJECTS=(
  "tests/Inovait.UnitTests/Inovait.UnitTests.csproj"
  "tests/Inovait.IntegrationTests/Inovait.IntegrationTests.csproj"
)
CLEAN_ENV=(env -u SONAR_TOKEN -u SONAR_TOKEN_VALUE)

cleanup_work_dir() {
  rm -rf -- "$SONAR_WORK_DIR"
}

cleanup_on_exit() {
  unset SONAR_TOKEN_VALUE
  cleanup_work_dir
}

trap cleanup_on_exit EXIT
cleanup_work_dir
mkdir -p "$RESULTS_DIR"

"${CLEAN_ENV[@]}" dotnet tool restore

"${CLEAN_ENV[@]}" dotnet sonarscanner begin \
  /k:"$SONAR_PROJECT_KEY" \
  /n:"$SONAR_PROJECT_NAME" \
  /d:sonar.host.url="$SONAR_HOST_URL" \
  /d:sonar.token="$SONAR_TOKEN_VALUE" \
  /d:sonar.cs.opencover.reportsPaths="$RESULTS_DIR/**/coverage.opencover.xml" \
  /d:sonar.cs.vstest.reportsPaths="$RESULTS_DIR/**/*.trx" \
  /d:sonar.exclusions="**/bin/**,**/obj/**,**/TestResults/**,**/.sonarqube/**,**/.agents/**,**/.atl/**,**/.claude/**,**/.playwright-mcp/**,**/.specify/**" \
  /d:sonar.cpd.exclusions="**/Persistence/Migrations/**" \
  /d:sonar.text.inclusions.activate="true" \
  /d:sonar.text.inclusions="**/*.md,**/*.http" \
  /d:sonar.python.version="3.14"

"${CLEAN_ENV[@]}" dotnet build "$SOLUTION" \
  --configuration "$CONFIGURATION" \
  --no-incremental \
  --disable-build-servers

for project in "${TEST_PROJECTS[@]}"; do
  project_name="$(basename "$project" .csproj)"
  project_results="$RESULTS_DIR/$project_name"
  mkdir -p "$project_results"

  "${CLEAN_ENV[@]}" dotnet test "$project" \
    --configuration "$CONFIGURATION" \
    --no-build \
    --no-restore \
    --results-directory "$project_results" \
    --logger "trx;LogFileName=$project_name.trx" \
    --collect:"XPlat Code Coverage;Format=opencover"
done

"${CLEAN_ENV[@]}" dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN_VALUE"

echo "SonarQube analysis submitted: $SONAR_HOST_URL/dashboard?id=$SONAR_PROJECT_KEY"
