namespace NamFix.SharedUi.Services;

/// <summary>
/// Which host is rendering the shared UI. The RCL runs unchanged in both the Blazor WASM web app and
/// the MAUI Blazor Hybrid mobile app, but a few things must differ (status-bar / notch safe areas,
/// touch-first affordances). Each host registers this so the UI can adapt at the edges without a
/// project reference back to the host.
/// </summary>
public enum HostPlatform
{
    Web,
    Mobile
}

/// <summary>
/// Ambient, read-only info about the current host platform. Registered as a singleton by
/// <c>AddNamFixSharedUi(HostPlatform)</c>; the web host passes <see cref="HostPlatform.Web"/> (default)
/// and the MAUI host passes <see cref="HostPlatform.Mobile"/>. Layout components tag the app root with
/// <c>data-platform</c> from this so CSS can target the mobile shell (see <c>namfix.css</c>).
/// </summary>
public sealed class AppPlatformInfo
{
    public AppPlatformInfo(HostPlatform platform) => Platform = platform;

    public HostPlatform Platform { get; }

    public bool IsMobile => Platform == HostPlatform.Mobile;
    public bool IsWeb => Platform == HostPlatform.Web;

    /// <summary>The value used for the root element's <c>data-platform</c> attribute ("web" / "mobile").</summary>
    public string Slug => IsMobile ? "mobile" : "web";
}
