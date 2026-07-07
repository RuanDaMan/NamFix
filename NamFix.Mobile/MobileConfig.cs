namespace NamFix.Mobile;

/// <summary>
/// Mobile host configuration. The API base URL defaults to the Android emulator's alias for the
/// host machine (<c>10.0.2.2</c>), which needs no setup. To target a <b>physical phone</b> (or a
/// deployed API), drop a gitignored <c>MobileConfig.Local.cs</c> next to this file that implements
/// <see cref="SetLocalApiBaseUrl"/> — see <c>MobileConfig.Local.cs.example</c>. That keeps the
/// machine-specific LAN IP out of source control, so nobody edits tracked code to switch targets.
/// </summary>
public static partial class MobileConfig
{
    /// <summary>Emulator default: 10.0.2.2 is the Android emulator's alias for the host loopback.</summary>
    private const string EmulatorApiBaseUrl = "https://10.0.2.2:7111/";

    /// <summary>
    /// Base URL of the NamFix Web API. Returns the local override when present, otherwise the
    /// emulator default.
    /// <list type="bullet">
    /// <item>Android emulator → PC: <c>https://10.0.2.2:7111/</c> (the built-in default).</item>
    /// <item>Physical phone → PC: <c>https://&lt;your-PC-LAN-IP&gt;:7111/</c> via the local override (same Wi-Fi; PC firewall must allow the port).</item>
    /// <item>Deployed API: the public HTTPS URL via the local override.</item>
    /// </list>
    /// The API's <c>Cors:Origins</c> allowlist does not apply here (native HTTP client, not a browser origin).
    /// </summary>
    public static string ApiBaseUrl
    {
        get
        {
            string? local = null;
            SetLocalApiBaseUrl(ref local);
            return string.IsNullOrWhiteSpace(local) ? EmulatorApiBaseUrl : local;
        }
    }

    /// <summary>
    /// Implemented by the optional, gitignored <c>MobileConfig.Local.cs</c> to point the app at a
    /// physical device / deployed API. If that file is absent the call is elided and the emulator
    /// default is used.
    /// </summary>
    static partial void SetLocalApiBaseUrl(ref string? url);
}
