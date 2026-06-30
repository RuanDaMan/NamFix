#requires -Version 5
# ---------------------------------------------------------------------------
# Deploy / migrate the NamFix database with Grate.
#
# Run this from the NamFix.Api folder:
#     .\deploydb.ps1
#
# It creates the NamFix database if it doesn't exist, runs the versioned `up/`
# scripts once, then re-applies views, sprocs, permissions and seed data.
# SQL lives in the Migrations/ folder next to this script (see Migrations/README.md).
# ---------------------------------------------------------------------------
$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

# The SQL files live in ./Migrations relative to this script (NamFix.Api).
$sqlDir = Resolve-Path (Join-Path $PSScriptRoot 'Migrations')

# Use the same connection string the app uses: ConnectionStrings:DefaultConnection
# from NamFix.Api/appsettings.json (single source of truth).
$appSettings = Get-Content (Join-Path $PSScriptRoot 'appsettings.json') -Raw | ConvertFrom-Json
$connectionString = $appSettings.ConnectionStrings.DefaultConnection
if (-not $connectionString) {
    throw "ConnectionStrings:DefaultConnection not found in appsettings.json."
}

# Grate is published for the .NET 8 runtime. If that runtime is missing, allow
# the host to roll forward to a newer major (e.g. .NET 10) so grate can still run.
if (-not $env:DOTNET_ROLL_FORWARD) { $env:DOTNET_ROLL_FORWARD = 'Major' }

# Make sure grate is available.
if (-not (Get-Command grate -ErrorAction SilentlyContinue)) {
    throw "grate is not installed. Run: dotnet tool install -g grate"
}

Write-Host "Deploying NamFix database via Grate" -ForegroundColor Cyan
Write-Host "  SQL files : $sqlDir"
Write-Host "  Target    : $($connectionString -replace 'Password=[^;]*', 'Password=***')"

# Folder config uses grate's short form (folderName=runType, ';'-separated).
# NOTE: the JSON form shown in Migrations/README.md is NOT understood by this grate
# version — it parses --folders with a custom ';'/'='/':' syntax instead.
$folders = "runAfterCreateDatabase=Once;up=Once;views=AnyTime;sprocs=AnyTime;permissions=EveryTime;seed=EveryTime"

# --silent: grate otherwise pauses for an interactive "press Enter" and hangs.
# Do NOT pass --transaction; the full-text catalog in up/0006 cannot run inside
# a transaction (see Migrations/README.md).
grate `
    --connectionstring="$connectionString" `
    --sqlfilesdirectory="$sqlDir" `
    --databasetype=sqlserver `
    --folders="$folders" `
    --silent

if ($LASTEXITCODE -ne 0) {
    throw "Grate failed with exit code $LASTEXITCODE."
}

Write-Host "Database deployment complete." -ForegroundColor Green
