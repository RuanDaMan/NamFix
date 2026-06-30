#requires -Version 5
# ---------------------------------------------------------------------------
# Run the NamFix backend API. It also serves the Blazor WASM client, so for
# normal use you only need THIS script — browse to the URL below.
#   API + app:  http://localhost:5244
#   OpenAPI:    http://localhost:5244/openapi/v1.json
# ---------------------------------------------------------------------------
$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

# Free port 5244 if a previous run (or a crashed instance) is still holding it.
$stale = Get-NetTCPConnection -LocalPort 5244 -State Listen -ErrorAction SilentlyContinue
if ($stale) {
    $stale.OwningProcess | Sort-Object -Unique | ForEach-Object {
        Write-Warning "Port 5244 already in use by PID $_ — stopping it."
        Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Milliseconds 500
}

# Pick a SQL Server to talk to. Prefer LocalDB if it's installed; otherwise fall
# back to a local full/Express instance (the default instance at "(local)").
$haveLocalDb = [bool](Get-Command sqllocaldb -ErrorAction SilentlyContinue)

if ($haveLocalDb) {
    # Make sure the LocalDB engine is running (no-op if already up).
    try { & sqllocaldb start MSSQLLocalDB | Out-Null }
    catch { Write-Warning "Could not start LocalDB (MSSQLLocalDB): $($_.Exception.Message)" }
}

# Default the DB connection unless it's already set in the environment.
if (-not $env:NAMFIX_ConnectionStrings__NamFix) {
    $server = if ($haveLocalDb) { "(localdb)\MSSQLLocalDB" } else { "(local)" }
    $env:NAMFIX_ConnectionStrings__NamFix = "Server=$server;Database=NamFix;Trusted_Connection=True;TrustServerCertificate=True;"
    Write-Host "Using DB connection: Server=$server;Database=NamFix" -ForegroundColor DarkGray
}

Write-Host "Starting NamFix API on http://localhost:5244 ..." -ForegroundColor Cyan
dotnet run --launch-profile http
