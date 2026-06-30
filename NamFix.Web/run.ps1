#requires -Version 5
# ---------------------------------------------------------------------------
# Run the NamFix Blazor WASM client on its own dev server (hot-reload friendly).
#
#   .\run.ps1            Local dev only:  https://localhost:7213
#   .\run.ps1 live       LAN access:      http://0.0.0.0:7213 (open this PC's
#                        IP on your phone's browser to use the app)
#
# It calls the API at the "ApiBaseUrl" in wwwroot/appsettings.json (default
# https://localhost:7111). In live mode the client automatically retargets that
# at this PC's IP, so start the backend the same way first:
#   ..\NamFix.Api\run.ps1 live
# Trust the dev cert once if you haven't (local mode only): dotnet dev-certs https --trust
# ---------------------------------------------------------------------------
param([string]$Mode)
$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

$live = $Mode -eq 'live'

function Get-LanIPv4 {
    $ip = $null
    try {
        $ip = (Find-NetRoute -RemoteIPAddress 8.8.8.8 -ErrorAction Stop |
               Where-Object { $_.IPAddress -and $_.IPAddress -ne '127.0.0.1' } |
               Select-Object -First 1).IPAddress
    } catch { }
    if (-not $ip) {
        $ip = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
               Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
               Sort-Object InterfaceMetric |
               Select-Object -First 1).IPAddress
    }
    return $ip
}

if ($live) {
    $ip = Get-LanIPv4
    if (-not $ip) { throw "Could not determine this PC's LAN IP address. Connect to a network and retry." }

    # Bind all interfaces over HTTP so other devices can load the client. The WASM client derives
    # the API host from the address it was served from, so no per-run config edit is needed.
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = 'http://0.0.0.0:7213'

    Write-Host "Starting NamFix client (LIVE) on http://0.0.0.0:7213" -ForegroundColor Cyan
    Write-Host "  Open this on your phone's browser: http://${ip}:7213" -ForegroundColor Green
    Write-Host "  (Make sure the API is running:      ..\NamFix.Api\run.ps1 live)" -ForegroundColor DarkGray
    Write-Host "  (Windows Firewall may prompt to allow access — choose Private networks.)" -ForegroundColor DarkGray
    dotnet run --no-launch-profile
}
else {
    Write-Host "Starting NamFix client on https://localhost:7213 (API expected at https://localhost:7111) ..." -ForegroundColor Cyan
    dotnet run --launch-profile https
}
