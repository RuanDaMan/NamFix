#requires -Version 5
# ---------------------------------------------------------------------------
# Run the NamFix Blazor WASM client on its OWN dev server (hot-reload friendly).
#   Client:  http://localhost:5157
# It calls the API at the "ApiBaseUrl" in wwwroot/appsettings.json
# (default http://localhost:5244), so start the backend first:
#   ..\NamFix.Api\run.ps1
#
# Note: for a normal run you don't need this — NamFix.Api already serves the
# compiled client. Use this only when iterating on the front-end standalone.
# ---------------------------------------------------------------------------
$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

Write-Host "Starting NamFix client on http://localhost:5157 (API expected at http://localhost:5244) ..." -ForegroundColor Cyan
dotnet run --launch-profile http
