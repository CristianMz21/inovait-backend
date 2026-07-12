<#
.SYNOPSIS
    Brings up the whole local Inovait stack for evaluators, or tears it down.

.DESCRIPTION
    Starts SQL Server (Docker), the Inovait API, and the Angular frontend
    built in production configuration talking real HTTP to the API (no
    mocks). Also tears the whole stack down cleanly with -Down.

    Written for Windows PowerShell 5.1 (no `??`, no `&&` chains, no
    PowerShell-7-only syntax).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\deploy-local.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\deploy-local.ps1 -ApiPort 5050 -SqlPort 1434

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\deploy-local.ps1 -SkipFrontend

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\deploy-local.ps1 -NoDemoData

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\deploy-local.ps1 -Down

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\deploy-local.ps1 -CheckOnly

.NOTES
    The generated (or supplied) SA password only ever lives in this
    process's environment and the environment of the child processes it
    starts -- it is NEVER written to a file. -Down does not need it: a
    dummy value is used only to satisfy docker compose variable
    interpolation while tearing down.
#>
[CmdletBinding()]
param(
    [int]$ApiPort = 5000,
    [int]$SqlPort = 1433,
    [string]$FrontendPath,
    [string]$SaPassword,
    [switch]$SkipFrontend,
    [switch]$NoDemoData,
    [switch]$Down,
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

if (-not $FrontendPath) {
    $FrontendPath = Join-Path $RepoRoot '..\inovait-frontend'
}

$StateDir = Join-Path $RepoRoot '.local-stack'
$ComposeService = 'sql-server'
$SqlCmdPath = '/opt/mssql-tools18/bin/sqlcmd'

$ApiPidFile = Join-Path $StateDir 'api.pid'
$FrontendPidFile = Join-Path $StateDir 'frontend.pid'
$ApiLogFile = Join-Path $StateDir 'api.log'
$ApiErrLogFile = Join-Path $StateDir 'api.err.log'
$FrontendLogFile = Join-Path $StateDir 'frontend.log'
$FrontendErrLogFile = Join-Path $StateDir 'frontend.err.log'

$script:SaPasswordGenerated = $false

# ---------------------------------------------------------------------------
# Small helpers
# ---------------------------------------------------------------------------

function Write-Info {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Green
}

function Write-WarningMsg {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Yellow
}

function Fail {
    param([string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor Red
    exit 1
}

function Test-CommandExists {
    param([string]$Name, [string]$Hint)
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $cmd) {
        Fail "Required command '$Name' not found. Install $Hint and retry."
    }
}

function Test-PortFree {
    param([int]$Port, [string]$Label, [string]$Suggestion)
    $listener = $null
    try {
        $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, $Port)
        $listener.Start()
    }
    catch {
        Fail "Port $Port ($Label) is already in use. $Suggestion"
    }
    finally {
        if ($listener) {
            $listener.Stop()
        }
    }
}

function Wait-ForHttp {
    param([string]$Url, [int]$TimeoutSeconds, [string]$Label, [string]$LogFile)
    $waited = 0
    $interval = 2
    Write-Host -NoNewline "Waiting for $Label to respond at $Url"
    while ($true) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                Write-Host " ready."
                return $true
            }
        }
        catch {
            # Not ready yet -- keep polling until the timeout.
        }
        if ($waited -ge $TimeoutSeconds) {
            Write-Host ''
            Write-Host "ERROR: $Label did not become ready within ${TimeoutSeconds}s ($Url)." -ForegroundColor Red
            if (Test-Path $LogFile) {
                Write-Host "---- last 40 lines of $LogFile ----"
                Get-Content $LogFile -Tail 40
            }
            return $false
        }
        Write-Host -NoNewline '.'
        Start-Sleep -Seconds $interval
        $waited += $interval
    }
}

function New-StrongPassword {
    # Guarantees upper/lower/digit/symbol characters so SQL Server's
    # password complexity policy (>= 3 of those 4 categories) is satisfied.
    $charset = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789'
    $symbols = '!@#%^*_+='
    $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    try {
        $bodyBytes = New-Object byte[] 20
        $rng.GetBytes($bodyBytes)
        $body = -join ($bodyBytes | ForEach-Object { $charset[$_ % $charset.Length] })

        $symBytes = New-Object byte[] 2
        $rng.GetBytes($symBytes)
        $symPart = -join ($symBytes | ForEach-Object { $symbols[$_ % $symbols.Length] })
    }
    finally {
        $rng.Dispose()
    }
    return "$body" + 'Aa1' + "$symPart"
}

# ---------------------------------------------------------------------------
# Prerequisite checks
# ---------------------------------------------------------------------------

function Invoke-PrereqChecks {
    Write-Info 'Checking prerequisites...'

    Test-CommandExists 'docker' 'Docker Desktop (https://docs.docker.com/get-docker/)'

    docker info *> $null
    if ($LASTEXITCODE -ne 0) {
        Fail 'Docker daemon is not running (or not reachable). Start Docker Desktop and retry.'
    }

    docker compose version *> $null
    if ($LASTEXITCODE -ne 0) {
        Fail "'docker compose' (v2 plugin) is not available. Update Docker Desktop."
    }

    Test-CommandExists 'dotnet' '.NET SDK (https://dotnet.microsoft.com/download)'

    $globalJsonPath = Join-Path $RepoRoot 'global.json'
    $globalJson = Get-Content $globalJsonPath -Raw | ConvertFrom-Json
    $requiredMajor = ($globalJson.sdk.version -split '\.')[0]
    $installedSdks = & dotnet --list-sdks 2>$null
    $haveMajor = $installedSdks | Where-Object { ($_ -split '\.')[0] -eq $requiredMajor }
    if (-not $haveMajor) {
        Fail "No installed .NET SDK matches major version $requiredMajor.x (required by global.json). Installed SDKs: $($installedSdks -join '; ')"
    }

    if (-not $SkipFrontend) {
        Test-CommandExists 'node' 'Node.js (https://nodejs.org/)'
        Test-CommandExists 'npm' 'npm (bundled with Node.js)'
        $nodeVersion = (& node --version).Trim()
        if ($nodeVersion -notmatch '^v24\.') {
            Write-WarningMsg "WARNING: Node.js v24.x is recommended; found $nodeVersion."
        }
    }

    Test-PortFree -Port $ApiPort -Label 'API' -Suggestion 'Choose a different port with -ApiPort.'
    Test-PortFree -Port $SqlPort -Label 'SQL Server' -Suggestion 'Choose a different port with -SqlPort.'
    if (-not $SkipFrontend) {
        Test-PortFree -Port 4200 -Label 'frontend' -Suggestion 'Stop whatever is using it, or re-run with -SkipFrontend.'
    }

    Write-Ok 'All prerequisite checks passed.'
}

# ---------------------------------------------------------------------------
# Stack steps
# ---------------------------------------------------------------------------

function Start-SqlServer {
    Write-Info 'Starting SQL Server container (this can take up to ~30s for the healthcheck to pass)...'
    # Kept for the whole script lifetime: docker compose re-interpolates the
    # compose file on EVERY invocation (up/cp/exec/down), not just `up`. The
    # process environment dies with this script and is never written to disk.
    $env:MSSQL_SA_PASSWORD = $SaPassword
    $env:INOVAIT_SQL_PORT = "$SqlPort"
    docker compose up -d --wait
    if ($LASTEXITCODE -ne 0) {
        Fail "SQL Server container failed to become healthy. Inspect it with: docker compose logs $ComposeService"
    }
    Write-Ok "SQL Server is up on port $SqlPort."
}

function Invoke-DatabaseSetup {
    Write-Info 'Preparing database (Inovait) and applying database/setup.sql...'

    docker compose cp database/setup.sql "${ComposeService}:/tmp/setup.sql"
    if ($LASTEXITCODE -ne 0) {
        Fail "Failed to copy database/setup.sql into the $ComposeService container."
    }

    # KNOWN GOTCHA: `docker compose cp` preserves the host file's uid/mode
    # (typically 0640, owned by a uid the container's mssql user cannot
    # read). Force it world-readable inside the container before sqlcmd
    # reads it.
    docker compose exec -T -u root $ComposeService chmod 0444 /tmp/setup.sql
    if ($LASTEXITCODE -ne 0) {
        Fail 'Failed to chmod /tmp/setup.sql inside the container.'
    }

    docker compose exec -T $ComposeService $SqlCmdPath -C -S localhost -U sa -P $SaPassword -b -Q "IF DB_ID('Inovait') IS NULL CREATE DATABASE Inovait"
    if ($LASTEXITCODE -ne 0) {
        Fail 'Failed to create the Inovait database.'
    }

    docker compose exec -T $ComposeService $SqlCmdPath -C -S localhost -U sa -P $SaPassword -b -d Inovait -i /tmp/setup.sql
    if ($LASTEXITCODE -ne 0) {
        Fail 'Failed to apply database/setup.sql.'
    }

    Write-Ok 'Database ready (setup.sql is idempotent -- safe on re-run).'
}

function Invoke-DemoDataSeed {
    if ($NoDemoData) {
        Write-Info 'Skipping demo data (-NoDemoData).'
        return
    }

    # WHY THIS EXISTS: the canonical production seed only contains
    # DocumentType 'CC' and no class groups/teachers, so the frontend
    # enrollment form (which offers DNI/PAS/CE per the contract examples)
    # always gets 404 and no walkthrough is possible without extra data.
    # This step seeds FICTITIOUS LOCAL-EVALUATION data only -- it is never
    # part of the production seed.
    Write-Info 'Seeding fictitious local-evaluation demo data (skip with -NoDemoData)...'

    # Pure ASCII on purpose: accented characters are emitted via NCHAR() so
    # the SQL survives every encoding layer (PS 5.1 source encoding,
    # Set-Content, docker cp, sqlcmd codepage).
    $demoSql = @'
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
'@

    $demoSqlFile = Join-Path $StateDir 'demo-data.sql'
    Set-Content -Path $demoSqlFile -Value $demoSql -Encoding Ascii

    docker compose cp $demoSqlFile "${ComposeService}:/tmp/demo-data.sql"
    if ($LASTEXITCODE -ne 0) {
        Fail "Failed to copy the demo data script into the $ComposeService container."
    }

    # Same uid/mode gotcha as setup.sql: make it readable for the mssql user.
    docker compose exec -T -u root $ComposeService chmod 0444 /tmp/demo-data.sql
    if ($LASTEXITCODE -ne 0) {
        Fail 'Failed to chmod /tmp/demo-data.sql inside the container.'
    }

    docker compose exec -T $ComposeService $SqlCmdPath -C -S localhost -U sa -P $SaPassword -b -d Inovait -i /tmp/demo-data.sql
    if ($LASTEXITCODE -ne 0) {
        Fail 'Failed to apply the demo data script.'
    }

    Write-Ok 'Demo data ready (idempotent -- per-row seeded/skipped summary above).'
}

function Start-Api {
    Write-Info 'Building API (Release)...'
    dotnet build src/Inovait.Api/Inovait.Api.csproj --configuration Release
    if ($LASTEXITCODE -ne 0) {
        Fail 'dotnet build failed.'
    }

    Write-Info "Starting API on http://localhost:$ApiPort ..."

    # KNOWN GOTCHA: `dotnet run` loads launchSettings.json and IGNORES
    # ASPNETCORE_URLS unless --no-launch-profile is passed -- that flag is
    # mandatory here, otherwise the API binds to the launchSettings ports.
    # TrustServerCertificate=True below targets ONLY this container's
    # self-signed dev certificate; this connection string is process-env
    # only and must never be committed to any config file.
    $connectionString = "Server=localhost,$SqlPort;Database=Inovait;User Id=sa;Password=$SaPassword;TrustServerCertificate=True"

    $env:ASPNETCORE_URLS = "http://localhost:$ApiPort"
    $env:ConnectionStrings__InovaitDatabase = $connectionString
    try {
        # Start-Process cannot merge stdout+stderr into a single file, so
        # the API's stderr goes to api.err.log alongside api.log.
        $proc = Start-Process -FilePath 'dotnet' `
            -ArgumentList 'run', '--project', 'src/Inovait.Api', '--configuration', 'Release', '--no-build', '--no-launch-profile' `
            -WorkingDirectory $RepoRoot `
            -RedirectStandardOutput $ApiLogFile `
            -RedirectStandardError $ApiErrLogFile `
            -PassThru -WindowStyle Hidden
        $proc.Id | Out-File -FilePath $ApiPidFile -Encoding ascii
    }
    finally {
        Remove-Item Env:\ASPNETCORE_URLS -ErrorAction SilentlyContinue
        Remove-Item Env:\ConnectionStrings__InovaitDatabase -ErrorAction SilentlyContinue
    }

    if (-not (Wait-ForHttp "http://localhost:$ApiPort/health/ready" 60 'API' $ApiLogFile)) {
        exit 1
    }
}

function Start-Frontend {
    if ($SkipFrontend) {
        Write-Info 'Skipping frontend (-SkipFrontend).'
        return
    }

    if (-not (Test-Path $FrontendPath -PathType Container)) {
        Fail "Frontend path not found: $FrontendPath. Pass -FrontendPath <path> or clone inovait-frontend next to this repo."
    }

    $nodeModules = Join-Path $FrontendPath 'node_modules'
    if (-not (Test-Path $nodeModules -PathType Container)) {
        Write-Info 'First run: installing frontend dependencies (npm ci) -- this can take a few minutes...'
        Push-Location $FrontendPath
        try {
            npm ci
            if ($LASTEXITCODE -ne 0) {
                Fail 'npm ci failed.'
            }
        }
        finally {
            Pop-Location
        }
    }

    Write-Info 'Starting frontend on http://localhost:4200 (production configuration) ...'

    # npx must be resolved to a concrete path -- Start-Process does not
    # search PATHEXT the way cmd.exe does for a bare command name.
    $npxCmd = (Get-Command npx -ErrorAction SilentlyContinue).Source
    if (-not $npxCmd) {
        Fail "Could not resolve 'npx' on PATH."
    }

    $proc = Start-Process -FilePath $npxCmd `
        -ArgumentList 'ng', 'serve', '--configuration', 'production', '--port', '4200', '--host', '127.0.0.1' `
        -WorkingDirectory $FrontendPath `
        -RedirectStandardOutput $FrontendLogFile `
        -RedirectStandardError $FrontendErrLogFile `
        -PassThru -WindowStyle Hidden
    $proc.Id | Out-File -FilePath $FrontendPidFile -Encoding ascii

    # The app must be opened via http://localhost:4200 (NOT 127.0.0.1:4200):
    # the API's CORS allowlist only contains the localhost origin, so a
    # 127.0.0.1 origin gets CORS-blocked. ng serve still binds 127.0.0.1 above.
    # The Angular dev-server production build can take 60-120s; be generous.
    if (-not (Wait-ForHttp 'http://localhost:4200' 180 'frontend' $FrontendLogFile)) {
        exit 1
    }
}

function Stop-ProcessFromPidFile {
    param([string]$PidFile, [string]$Label)
    if (Test-Path $PidFile) {
        $procId = Get-Content $PidFile -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($procId) {
            $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
            if ($proc) {
                Write-Info "Stopping $Label (pid $procId)..."
                try {
                    Stop-Process -Id $procId -Force -ErrorAction Stop
                }
                catch {
                    Write-WarningMsg "Could not stop $Label (pid $procId): $($_.Exception.Message)"
                }
            }
        }
        Remove-Item $PidFile -ErrorAction SilentlyContinue
    }
}

function Stop-Stack {
    Write-Info 'Tearing down local stack...'
    Stop-ProcessFromPidFile -PidFile $ApiPidFile -Label 'API'
    Stop-ProcessFromPidFile -PidFile $FrontendPidFile -Label 'frontend'

    Test-CommandExists 'docker' 'Docker Desktop (https://docs.docker.com/get-docker/)'

    # docker compose requires MSSQL_SA_PASSWORD for variable interpolation
    # even to tear down -- a dummy value is fine, it is never used for auth.
    $hadPassword = [bool]$env:MSSQL_SA_PASSWORD
    if (-not $hadPassword) {
        $env:MSSQL_SA_PASSWORD = 'teardown-only-unused'
    }
    try {
        docker compose down -v
    }
    finally {
        if (-not $hadPassword) {
            Remove-Item Env:\MSSQL_SA_PASSWORD -ErrorAction SilentlyContinue
        }
    }

    Write-Ok 'Stack is down. (.local-stack\*.log kept for inspection; safe to delete.)'
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

try {
    if (-not (Test-Path $StateDir)) {
        New-Item -ItemType Directory -Path $StateDir | Out-Null
    }

    if ($Down) {
        Stop-Stack
        exit 0
    }

    if ($CheckOnly) {
        Invoke-PrereqChecks
        exit 0
    }

    Invoke-PrereqChecks

    if (-not $SaPassword) {
        $SaPassword = New-StrongPassword
        $script:SaPasswordGenerated = $true
    }

    Start-SqlServer
    Invoke-DatabaseSetup
    Invoke-DemoDataSeed
    Start-Api
    Start-Frontend

    Write-Host ''
    Write-Host '================================================================'
    Write-Host ' Inovait local stack is up' -ForegroundColor Green
    Write-Host '================================================================'
    if ($script:SaPasswordGenerated) {
        Write-WarningMsg 'Generated SQL Server SA password (shown once, never written to disk):'
        Write-Host "  $SaPassword"
        Write-Host 'You do not need it to tear the stack down.'
        Write-Host ''
    }
    if ($SkipFrontend) {
        Write-Host 'Frontend:   skipped (-SkipFrontend)'
    }
    else {
        # localhost, not 127.0.0.1: the API's CORS allowlist only has this origin.
        Write-Host 'Frontend:   http://localhost:4200'
    }
    if ($NoDemoData) {
        Write-Host 'Demo data:  skipped (-NoDemoData)'
    }
    else {
        Write-Host 'Demo data:  seeded (fictitious local-evaluation data only)'
    }
    Write-Host "API:        http://localhost:$ApiPort"
    Write-Host "Health:     http://localhost:$ApiPort/health/ready"
    Write-Host "Logs:       $ApiLogFile / $FrontendLogFile (stderr in the matching .err.log)"
    Write-Host "Teardown:   powershell -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Down"
    Write-Host '================================================================'
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
