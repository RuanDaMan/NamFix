# NamFix.Mobile (reference scaffold — not yet implemented)

This is the placeholder for the future **.NET MAUI Blazor Hybrid** app. It is intentionally
**not built** yet and is **not added to `NamFix.sln`** (the MAUI workload is not required to build
the rest of the solution). It documents exactly how the mobile app will reuse the existing UI.

## Why so little here

Almost everything the mobile app needs already lives in shared projects:

- **UI**: every page, layout, and component is in **`NamFix.SharedUi`** (a Razor Class Library).
  The MAUI app renders them inside a `BlazorWebView` — no rewrite.
- **Contracts & DTOs**: **`NamFix.Shared`**.
- **API access**: **`NamFix.SharedUi.Services.NamFixApiClient`** talks to the same Web API.

## Intended wiring

1. Create the project (requires the MAUI workload: `dotnet workload install maui`):

   ```bash
   dotnet new maui-blazor -n NamFix.Mobile -o NamFix.Mobile
   ```

2. Reference the shared projects:

   ```bash
   dotnet add NamFix.Mobile reference ../NamFix.SharedUi/NamFix.SharedUi.csproj
   dotnet add NamFix.Mobile reference ../NamFix.Shared/NamFix.Shared.csproj
   ```

3. In `MauiProgram.cs`, register the shared UI plus host-specific implementations of the
   abstractions from `NamFix.Shared.Contracts`:

   ```csharp
   builder.Services.AddMauiBlazorWebView();
   builder.Services.AddNamFixSharedUi();                      // shared UI services

   // Host-specific implementations (these differ from the web host):
   builder.Services.AddScoped<ITokenStore, SecureStorageTokenStore>();   // MAUI SecureStorage
   builder.Services.AddHttpClient<NamFixApiClient>(c =>
       c.BaseAddress = new Uri("https://your-api-host/"))
       .AddHttpMessageHandler<AuthHeaderHandler>();
   ```

   `IMapService` (Leaflet) and `IGeolocationService` already work over the `BlazorWebView`, so they
   are reused as-is from `NamFix.SharedUi`. Only `ITokenStore` (SecureStorage) and the API base URL
   need a mobile-specific implementation.

4. Point `Components/Routes.razor` at the shared assembly via `AdditionalAssemblies`
   (same pattern as `NamFix.Web/App.razor`).

5. Reference the shared CSS and Leaflet CDN in `wwwroot/index.html`, exactly like
   `NamFix.Web/wwwroot/index.html`.

## What stays platform-specific

Only the implementations behind the `NamFix.Shared.Contracts` interfaces:
`ITokenStore` (SecureStorage), and later `ISecureStorage`, file pickers, and push notifications.
