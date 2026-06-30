#requires -Version 5
# ---------------------------------------------------------------------------
# Deploy / migrate the NamFix database with Grate.
#
# Run this from the NamFix.Api folder:
#     .\deploydb.ps1
#
# It creates the NamFix database if it doesn't exist, runs the versioned `up/`
# scripts once, then re-applies views, sprocs, permissions and seed data.
# SQL lives in the repo's top-level db/ folder (see db/README.md).
# ---------------------------------------------------------------------------
$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

# The SQL files live in ../db relative to this script (NamFix.Api).
$sqlDir = Resolve-Path (Join-Path $PSScriptRoot '..\db')

# Pick a SQL Server to talk to. Honour an explicit connection string if set,
# otherwise prefer LocalDB when installed and fall back to the local default
# instance "(local)". This mirrors the logic in run.ps1.
if ($env:NAMFIX_ConnectionStrings__NamFix) {
    $connectionString = $env:NAMFIX_ConnectionStrings__NamFix
} else {
    $haveLocalDb = [bool](Get-Command sqllocaldb -ErrorAction SilentlyContinue)
    if ($haveLocalDb) {
        try { & sqllocaldb start MSSQLLocalDB | Out-Null } catch { }
        $server = "(localdb)\MSSQLLocalDB"
    } else {
        $server = "(local)"
    }
    $connectionString = "Server=$server;Database=NamFix;Trusted_Connection=True;TrustServerCertificate=True;"
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
# NOTE: the JSON form shown in db/README.md is NOT understood by this grate
# version — it parses --folders with a custom ';'/'='/':' syntax instead.
$folders = "runAfterCreateDatabase=Once;up=Once;views=AnyTime;sprocs=AnyTime;permissions=EveryTime;seed=EveryTime"

# --silent: grate otherwise pauses for an interactive "press Enter" and hangs.
# Do NOT pass --transaction; the full-text catalog in up/0006 cannot run inside
# a transaction (see db/README.md).
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
