# NamFix.Mobile (.NET MAUI Blazor Hybrid — Android)

The mobile app. It **reuses the shared UI unchanged**: every page, layout, and component lives in
**`NamFix.SharedUi`** (a Razor Class Library) and is rendered inside a `BlazorWebView`. Only the
host-specific bits differ (token storage, API base URL). Contracts & DTOs come from
**`NamFix.Shared`**; API access goes through **`NamFix.SharedUi.Services.NamFixApiClient`**.

Currently targets **Android only** (`net10.0-android`), matching the installed `maui-android`
workload. It **is** part of `NamFix.sln`, so building the whole solution now requires the MAUI
workload — see setup below.

## How the shared UI is wired

Mirrors `NamFix.Web` exactly:

- **`MauiProgram.cs`** registers `AddMauiBlazorWebView()` + `AddNamFixSharedUi()`, then the
  host-specific pieces: `SecureStorageTokenStore` (implements `ITokenStore` over MAUI SecureStorage),
  the `NamFixApiClient` HttpClient (base URL + `AuthHeaderHandler`), and the `ConnectivityService` /
  `NotificationService` SignalR trackers.
- **`Components/Routes.razor`** pulls the RCL routes in via `AdditionalAssemblies`
  (`typeof(NamFix.SharedUi.Pages.Home).Assembly`) and uses the shared `MainLayout` — same pattern as
  `NamFix.Web/App.razor`.
- **`wwwroot/index.html`** loads the RCL's `namfix.css` + `namfix.js` and the Leaflet CDN, identical
  to the web host. `IMapService` (Leaflet) and `IGeolocationService` work over the `BlazorWebView`
  and are reused as-is.
- **`MobileConfig.cs`** holds the one thing you tune per environment: `ApiBaseUrl`.

## Connecting to the API

The API base URL is `MobileConfig.ApiBaseUrl` (default `https://10.0.2.2:7111/`).

| Scenario                | Set `ApiBaseUrl` to                     | Notes                                                        |
|-------------------------|-----------------------------------------|-------------------------------------------------------------|
| Android **emulator**    | `https://10.0.2.2:7111/`                | `10.0.2.2` is the emulator's alias for the PC's loopback.    |
| **Physical phone**      | `https://<your-PC-LAN-IP>:7111/`        | Phone + PC on the same Wi-Fi; allow the port through the PC firewall. |
| Deployed API            | the public HTTPS URL                    | —                                                           |

- **Dev TLS:** in **DEBUG** builds `MauiProgram` attaches a handler that accepts the local
  mkcert/self-signed dev certificate (Android won't trust it otherwise). This is compiled out of
  Release. For a physical phone over **http**, also add `android:usesCleartextTraffic="true"` to the
  Android `<application>` (dev only) or install your mkcert root CA on the device.
- Ensure the API is actually listening on the LAN, not just `localhost` (see the HTTPS-for-LAN setup
  in the repo).

## First-time toolchain setup (already done in this repo's environment)

For reference / a fresh machine:

```powershell
# 1. MAUI Android workload
dotnet workload install maui-android

# 2. Android SDK + JDK 17 (auto-installs to %LOCALAPPDATA%\Android\Sdk, accepts licenses)
dotnet build NamFix.Mobile/NamFix.Mobile.csproj -t:InstallAndroidDependencies `
    -f net10.0-android -p:AcceptAndroidSDKLicenses=True

# 3. Emulator system image + an AVD (one-time; see scripts/ or the commands in the setup notes)
```

## Troubleshooting: `XA5207: Could not find android.jar for API level 36`

If a build fails with this even though `%LOCALAPPDATA%\Android\Sdk\platforms\android-36\android.jar`
exists, it's almost always a **poisoned MSBuild worker node**, not a real SDK problem. MSBuild keeps
worker nodes alive and reuses them; a node that was first spawned *before* the android-36 platform
finished installing caches "no platforms" in memory and keeps serving that to every reused build
(terminal and Rider alike), while freshly-spawned processes succeed. `dotnet build-server shutdown`
alone does **not** fix it — node-reuse workers are separate and survive it.

Fix (one-time — the SDK is stable now, so it won't recur):

```powershell
dotnet build-server shutdown
# kill any lingering MSBuild worker nodes (run per elevation level you built from):
Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'MSBuild.exe' -or ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -match '/nodemode:') } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
```

Then rebuild. In Rider, closing and reopening it spawns fresh nodes. As a quick check, a single build
with an explicit `-f net10.0-android` routes to a different node and usually works even while a
poisoned node lingers.

## Running from Rider

1. **Point Rider at the SDK/JDK** — *Settings → Build, Execution, Deployment → Android* (and *… →
   Toolset and Build → MSBuild / JDK*). Set the Android SDK to `%LOCALAPPDATA%\Android\Sdk` and the
   JDK to the OpenJDK 17 that `InstallAndroidDependencies` placed (Rider usually auto-detects both).
2. **Pick the run configuration** — select `NamFix.Mobile` with the `net10.0-android` target; the
   device dropdown lists your AVDs and any connected phone.
3. **Emulator** — create/launch an AVD from Rider's device dropdown (*Device Manager*), then Run.
4. **Physical phone** — enable *Developer options → USB debugging*, plug in over USB, accept the RSA
   prompt, then pick the device in the dropdown and Run. Set `ApiBaseUrl` to your PC's LAN IP first.

## What stays platform-specific

Only the implementations behind the `NamFix.Shared.Contracts` interfaces: `ITokenStore`
(`SecureStorageTokenStore`), and later file pickers / push notifications.
