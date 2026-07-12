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

    # The demo data lives as a versioned deliverable script so evaluators can
    # also apply it standalone: database\seed-demo.sql (pure ASCII; accented
    # characters via NCHAR() so it survives every encoding layer). See
    # docs\SEED_DATA.md for the full dataset and reset-demo.sql for cleanup.
    $demoSqlFile = Join-Path $RepoRoot 'database\seed-demo.sql'
    if (-not (Test-Path $demoSqlFile)) {
        Fail "Missing $demoSqlFile (versioned demo data script)."
    }

    docker compose cp $demoSqlFile "${ComposeService}:/tmp/seed-demo.sql"
    if ($LASTEXITCODE -ne 0) {
        Fail "Failed to copy the demo data script into the $ComposeService container."
    }

    # Same uid/mode gotcha as setup.sql: make it readable for the mssql user.
    docker compose exec -T -u root $ComposeService chmod 0444 /tmp/seed-demo.sql
    if ($LASTEXITCODE -ne 0) {
        Fail 'Failed to chmod /tmp/seed-demo.sql inside the container.'
    }

    docker compose exec -T $ComposeService $SqlCmdPath -C -S localhost -U sa -P $SaPassword -b -d Inovait -i /tmp/seed-demo.sql
    if ($LASTEXITCODE -ne 0) {
        Fail 'Failed to apply the demo data script.'
    }

    Write-Ok 'Demo data ready (idempotent -- per-block seeded summary above; see docs\SEED_DATA.md).'
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
