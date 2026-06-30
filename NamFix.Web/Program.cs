using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;
using NamFix.Shared.Contracts;
using NamFix.SharedUi;
using NamFix.SharedUi.Services;
using NamFix.Web;
using NamFix.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Host-specific implementation of the token store (localStorage on web).
builder.Services.AddScoped<ITokenStore, LocalStorageTokenStore>();

// Shared UI services (auth state, API client, map/geolocation, auth header handler).
builder.Services.AddNamFixSharedUi();

// API base URL: same origin when hosted by NamFix.Api, or override via wwwroot/appsettings.json ("ApiBaseUrl").
var apiBase = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddHttpClient<NamFixApiClient>(client => client.BaseAddress = new Uri(apiBase))
    .AddHttpMessageHandler<AuthHeaderHandler>();

// Backend connectivity tracker (SignalR /hubs/status). Needs the API base address, so it's wired here.
builder.Services.AddScoped(sp => new ConnectivityService(
    new Uri(apiBase),
    sp.GetRequiredService<ILogger<ConnectivityService>>()));

await builder.Build().RunAsync();
