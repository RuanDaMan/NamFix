#requires -Version 5
# ---------------------------------------------------------------------------
# Run the NamFix backend API (REST + SignalR only — it no longer serves the
# client). Start the front-end separately with ..\NamFix.Web\run.ps1.
#
#   .\run.ps1            Local dev only:  https://localhost:7111
#   .\run.ps1 live       LAN access:      https://0.0.0.0:7111 (reachable from
#                        other devices on your network, e.g. your phone)
#
#   OpenAPI: <base>/openapi/v1.json
# Local mode uses the .NET dev cert — trust it once: dotnet dev-certs https --trust
# Live mode uses an mkcert cert so phones trust HTTPS/WSS. Install once:
#     winget install FiloSottile.mkcert    (then:  mkcert -install)
#   and install the same root CA on each phone: https://github.com/FiloSottile/mkcert#mobile-devices
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

# Ensure an mkcert-issued certificate exists for localhost + this PC's LAN IP, so phones that trust
# the mkcert root CA get warning-free HTTPS (and working WSS/SignalR). Regenerated each run because
# the LAN IP can change between networks. Requires mkcert on PATH and its root CA installed per device.
function New-LanCert {
    param([string]$Ip, [string]$CertDir)

    if (-not (Get-Command mkcert -ErrorAction SilentlyContinue)) {
        throw @'
mkcert is not installed (or not on PATH). Live HTTPS needs it. Install once:
    winget install FiloSottile.mkcert
    mkcert -install
Then install the SAME root CA on each phone that will connect:
    https://github.com/FiloSottile/mkcert#mobile-devices
'@
    }

    # Make sure the local root CA is present/trusted on this machine (idempotent; may prompt once).
    & mkcert -install 2>&1 | Out-Null

    if (-not (Test-Path $CertDir)) { New-Item -ItemType Directory -Path $CertDir | Out-Null }
    $cert = Join-Path $CertDir 'namfix.pem'
    $key  = Join-Path $CertDir 'namfix-key.pem'

    & mkcert -cert-file $cert -key-file $key localhost 127.0.0.1 ::1 $Ip 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "mkcert failed to issue a certificate for $Ip." }

    return @{ Cert = $cert; Key = $key }
}

# Recursively kill a process and all of its descendants. dotnet watch spawns a
# child `dotnet run`, which spawns the actual Kestrel server — killing only the
# top process leaves that grandchild orphaned (the zombie holding the port).
function Stop-ProcessTree {
    param([int]$ParentId)
    Get-CimInstance Win32_Process -Filter "ParentProcessId = $ParentId" -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-ProcessTree -ParentId $_.ProcessId }
    Stop-Process -Id $ParentId -Force -ErrorAction SilentlyContinue
}

# Backstop: kill whatever is still listening on the port after the tree is gone
# (catches any orphan whose parent chain was already broken).
function Stop-PortListeners {
    param([int]$Port)
    Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique |
        ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }
}

# Start dotnet as a tracked child and wait on it. On Ctrl+C, PowerShell runs the
# finally block, which tears down the whole process tree so nothing is left behind.
function Start-Dotnet {
    param([string[]]$DotnetArgs, [int]$Port)
    $proc = Start-Process -FilePath 'dotnet' -ArgumentList $DotnetArgs -PassThru -NoNewWindow
    try {
        Wait-Process -Id $proc.Id
    }
    finally {
        Write-Host "`nShutting down — stopping dotnet watch and child processes..." -ForegroundColor Yellow
        Stop-ProcessTree -ParentId $proc.Id
        Stop-PortListeners -Port $Port
    }
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

    # Bind all interfaces over HTTPS using an mkcert certificate that covers this PC's LAN IP, so
    # phones trusting the mkcert root CA get warning-free HTTPS and working SignalR (WSS). Kestrel
    # picks the cert up from the Kestrel:Certificates:Default config section (set via env vars below).
    $tls = New-LanCert -Ip $ip -CertDir (Join-Path $PSScriptRoot 'certs')
    $env:Kestrel__Certificates__Default__Path    = $tls.Cert
    $env:Kestrel__Certificates__Default__KeyPath = $tls.Key
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = 'https://0.0.0.0:7111'
    # Allow the LAN web origin through CORS (appended after the localhost origin in appsettings.json).
    $env:NAMFIX_Cors__Origins__1 = "https://${ip}:7213"

    Write-Host "Starting NamFix API (LIVE) on https://0.0.0.0:7111" -ForegroundColor Cyan
    Write-Host "  Reachable from this network at: https://${ip}:7111" -ForegroundColor Green
    Write-Host "  Allowing web origin via CORS:   https://${ip}:7213" -ForegroundColor DarkGray
    Write-Host "  (Windows Firewall may prompt to allow access — choose Private networks.)" -ForegroundColor DarkGray
    Start-Dotnet -DotnetArgs @('watch', 'run', '--no-launch-profile') -Port 7111
}
else {
    Write-Host "Starting NamFix API on https://localhost:7111 ..." -ForegroundColor Cyan
    Start-Dotnet -DotnetArgs @('watch', 'run', '--launch-profile', 'https') -Port 7111
}
