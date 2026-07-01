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

# Recursively kill a process and all of its descendants. dotnet watch spawns a
# child `dotnet run`, which spawns the actual dev server — killing only the top
# process leaves that grandchild orphaned (the zombie holding the port).
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
    Start-Dotnet -DotnetArgs @('watch', 'run', '--no-launch-profile') -Port 7213
}
else {
    Write-Host "Starting NamFix client on https://localhost:7213 (API expected at https://localhost:7111) ..." -ForegroundColor Cyan
    Start-Dotnet -DotnetArgs @('watch', 'run', '--launch-profile', 'https') -Port 7213
}
