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
// When the app is opened over the LAN (e.g. from a phone at http://<pc-ip>:7213), a configured
// localhost API URL is unreachable from the other device, so we retarget it at the same host/scheme
// the page was served from, keeping the configured API port. Plain localhost dev is unaffected.
var apiBase = ResolveApiBase(builder.Configuration["ApiBaseUrl"], builder.HostEnvironment.BaseAddress);
builder.Services.AddHttpClient<NamFixApiClient>(client => client.BaseAddress = new Uri(apiBase))
    .AddHttpMessageHandler<AuthHeaderHandler>();

// Backend connectivity tracker (SignalR /hubs/status). Needs the API base address, so it's wired here.
builder.Services.AddScoped(sp => new ConnectivityService(
    new Uri(apiBase),
    sp.GetRequiredService<ILogger<ConnectivityService>>()));

// Live notifications + booking updates (SignalR /hubs/notifications). Also needs the API base address.
builder.Services.AddScoped(sp => new NotificationService(
    new Uri(apiBase),
    sp.GetRequiredService<ITokenStore>(),
    sp.GetRequiredService<NamFixApiClient>(),
    sp.GetRequiredService<ILogger<NotificationService>>()));

await builder.Build().RunAsync();

// Resolve the API base address, retargeting a localhost URL at the browser's host when the page is
// served from another device (LAN/phone access). Loopback browser access keeps the configured value.
static string ResolveApiBase(string? configured, string baseAddress)
{
    if (string.IsNullOrWhiteSpace(configured))
        return baseAddress;

    var api = new Uri(configured);
    var browser = new Uri(baseAddress);

    if (api.IsLoopback && !browser.IsLoopback)
        return new UriBuilder(api) { Scheme = browser.Scheme, Host = browser.Host }.Uri.ToString();

    return configured;
}
