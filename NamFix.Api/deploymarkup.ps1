#requires -Version 5
# ---------------------------------------------------------------------------
# Load MARKUP (mock) TEST DATA into the NamFix database.
#
# Run this from the NamFix.Api folder, AFTER deploydb.ps1 has created the schema:
#     .\deploymarkup.ps1
#
# It runs ONLY the Migrations/markup/ folder (extra clients, providers, reviews
# and transactions for testing). The scripts are idempotent (MERGE), so it is safe
# to re-run. This data is intentionally separate from Migrations/seed and is NOT
# applied by deploydb.ps1.
# ---------------------------------------------------------------------------
$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

# Grate folders live under ./Migrations; markup is the Migrations/markup subfolder.
$sqlDir = Resolve-Path (Join-Path $PSScriptRoot 'Migrations')

# Use the same connection string the app uses: ConnectionStrings:DefaultConnection
# from NamFix.Api/appsettings.json (single source of truth).
$appSettings = Get-Content (Join-Path $PSScriptRoot 'appsettings.json') -Raw | ConvertFrom-Json
$connectionString = $appSettings.ConnectionStrings.DefaultConnection
if (-not $connectionString) {
    throw "ConnectionStrings:DefaultConnection not found in appsettings.json."
}

# Grate is published for the .NET 8 runtime; allow roll-forward if only newer is present.
if (-not $env:DOTNET_ROLL_FORWARD) { $env:DOTNET_ROLL_FORWARD = 'Major' }

if (-not (Get-Command grate -ErrorAction SilentlyContinue)) {
    throw "grate is not installed. Run: dotnet tool install -g grate"
}

Write-Host "Loading NamFix markup (test) data via Grate" -ForegroundColor Cyan
Write-Host "  SQL files : $sqlDir\markup"
Write-Host "  Target    : $($connectionString -replace 'Password=[^;]*', 'Password=***')"

# Only the markup folder, run EveryTime. Short-form folder syntax (the JSON form
# in Migrations/README.md is not understood by this grate version). --silent avoids the
# interactive "press Enter" prompt that otherwise hangs the run.
grate `
    --connectionstring="$connectionString" `
    --sqlfilesdirectory="$sqlDir" `
    --databasetype=sqlserver `
    --folders="markup=EveryTime" `
    --silent

if ($LASTEXITCODE -ne 0) {
    throw "Grate failed with exit code $LASTEXITCODE."
}

Write-Host "Markup test data loaded." -ForegroundColor Green
