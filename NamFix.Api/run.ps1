#requires -Version 5
# ---------------------------------------------------------------------------
# Run the NamFix backend API (REST + SignalR only — it no longer serves the
# client). Start the front-end separately with ..\NamFix.Web\run.ps1.
#
#   .\run.ps1            Local dev only:  https://localhost:7111
#   .\run.ps1 live       LAN access:      http://0.0.0.0:7111 (reachable from
#                        other devices on your network, e.g. your phone)
#
#   OpenAPI: <base>/openapi/v1.json
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

# Free port 7111 if a previous run (or a crashed instance) is still holding it.
$stale = Get-NetTCPConnection -LocalPort 7111 -State Listen -ErrorAction SilentlyContinue
if ($stale) {
    $stale.OwningProcess | Sort-Object -Unique | ForEach-Object {
        Write-Warning "Port 7111 already in use by PID $_ — stopping it."
        Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Milliseconds 500
}

# The database connection string comes from NamFix.Api/appsettings.json
# (ConnectionStrings:DefaultConnection). Edit it there to point at your SQL Server.
if ($live) {
    $ip = Get-LanIPv4
    if (-not $ip) { throw "Could not determine this PC's LAN IP address. Connect to a network and retry." }

    # Bind all interfaces over HTTP. HTTPS is intentionally skipped in live mode: other devices
    # (phones) don't trust this PC's dev certificate, which would break HTTPS and SignalR (WSS).
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = 'http://0.0.0.0:7111'
    # Allow the LAN web origin through CORS (appended after the localhost origin in appsettings.json).
    $env:NAMFIX_Cors__Origins__1 = "http://${ip}:7213"

    Write-Host "Starting NamFix API (LIVE) on http://0.0.0.0:7111" -ForegroundColor Cyan
    Write-Host "  Reachable from this network at: http://${ip}:7111" -ForegroundColor Green
    Write-Host "  Allowing web origin via CORS:   http://${ip}:7213" -ForegroundColor DarkGray
    Write-Host "  (Windows Firewall may prompt to allow access — choose Private networks.)" -ForegroundColor DarkGray
    dotnet run --no-launch-profile
}
else {
    Write-Host "Starting NamFix API on https://localhost:7111 ..." -ForegroundColor Cyan
    dotnet run --launch-profile https
}
