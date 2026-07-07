using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NamFix.Shared.Contracts;
using NamFix.SharedUi.Services;

namespace NamFix.SharedUi;

/// <summary>
/// Registers the host-agnostic UI services. The host is responsible for registering the HttpClient
/// (with <see cref="Services.AuthHeaderHandler"/>) and an <see cref="ITokenStore"/> implementation
/// (localStorage on web, SecureStorage on MAUI).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNamFixSharedUi(
        this IServiceCollection services, HostPlatform platform = HostPlatform.Web)
    {
        // Which host is rendering us (web vs MAUI mobile). Lets the shared UI adapt at the edges
        // (status-bar safe areas, touch affordances) without referencing the host project.
        services.AddSingleton(new AppPlatformInfo(platform));

        services.AddAuthorizationCore();
        services.AddScoped<NamFixAuthStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<NamFixAuthStateProvider>());

        // Central API-error sink (logs full detail, raises the short message to the UI as a toast).
        services.AddScoped<ApiErrorNotifier>();

        // UI theme (dark/light), persisted in localStorage. Host-agnostic (JS interop only).
        services.AddScoped<ThemeService>();

        // Tracks seen open-jobs so the provider's Job board badge shows only unseen ones.
        services.AddScoped<JobBoardState>();
        // ConnectivityService (SignalR backend heartbeat) is registered by the host, which knows the
        // API base address — see AddNamFixSharedUi callers.
        // NamFixApiClient itself is registered by the host via AddHttpClient<NamFixApiClient>()
        // so it receives an HttpClient configured with the API base address + AuthHeaderHandler.

        // Map + geolocation work over a webview in both hosts, so they live here.
        services.AddScoped<IMapService, LeafletMapService>();
        services.AddScoped<IGeolocationService, BrowserGeolocationService>();

        // The auth header handler needs the token store the host registers.
        services.AddScoped<AuthHeaderHandler>();

        return services;
    }
}
