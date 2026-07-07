using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using NamFix.Mobile.Services;
using NamFix.Shared.Contracts;
using NamFix.SharedUi;
using NamFix.SharedUi.Services;

namespace NamFix.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		// --- NamFix shared UI wiring (mirrors NamFix.Web/Program.cs) ---------------------------

		// Host-specific token store: platform SecureStorage instead of the web's localStorage.
		builder.Services.AddSingleton(SecureStorage.Default);
		builder.Services.AddScoped<ITokenStore, SecureStorageTokenStore>();

		// Shared UI services (auth state, map/geolocation, theme, auth header handler, error notifier).
		// Tag this host as the mobile platform so the shared UI applies the touch shell + safe areas.
		builder.Services.AddNamFixSharedUi(HostPlatform.Mobile);

		// Override the web's WebView-based geolocation with the native MAUI one so "use my current
		// location" prompts for permission IN-APP (system dialog) instead of failing and sending the
		// user out to OS settings. Registered after AddNamFixSharedUi so this wins.
		builder.Services.AddScoped<IGeolocationService, NamFix.Mobile.Services.MauiGeolocationService>();

		// API base URL. On the Android EMULATOR, 10.0.2.2 is an alias for the host machine's
		// loopback, so this reaches an API running on your PC. For a PHYSICAL phone, change this to
		// your PC's LAN IP (e.g. https://192.168.x.x:7111/) — the phone and PC must be on the same
		// network. See NamFix.Mobile/README.md for the full connectivity notes.
		var apiBase = new Uri(MobileConfig.ApiBaseUrl);

		builder.Services
			.AddHttpClient<NamFixApiClient>(client => client.BaseAddress = apiBase)
			.AddHttpMessageHandler<AuthHeaderHandler>()
			// Trust the local mkcert/dev HTTPS certificate that Android doesn't know — but only for
			// local/dev hosts (loopback, the emulator's 10.0.2.2, private LAN). A real deployed API
			// still gets full validation, so this is safe in Debug AND Release. See DevHttps.
			.ConfigurePrimaryHttpMessageHandler(DevHttps.CreateRestHandler);

		// Backend connectivity tracker (SignalR /hubs/status) — needs the API base address. The
		// DevHttps configurator lets the hub's own TLS handshake accept the local dev certificate
		// too (the REST cert trust above does not apply to SignalR's connection/WebSocket).
		builder.Services.AddScoped(sp => new ConnectivityService(
			apiBase,
			sp.GetRequiredService<ILogger<ConnectivityService>>(),
			DevHttps.ConfigureSignalR));

		// Live notifications + booking updates (SignalR /hubs/notifications) — needs the API base address.
		builder.Services.AddScoped(sp => new NotificationService(
			apiBase,
			sp.GetRequiredService<ITokenStore>(),
			sp.GetRequiredService<NamFixApiClient>(),
			sp.GetRequiredService<ILogger<NotificationService>>(),
			DevHttps.ConfigureSignalR));

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
