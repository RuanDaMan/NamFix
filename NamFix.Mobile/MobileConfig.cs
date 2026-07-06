namespace NamFix.Mobile;

/// <summary>
/// Mobile host configuration. Kept in one place so switching between the emulator and a physical
/// device (or a deployed API) is a one-line change.
/// </summary>
public static class MobileConfig
{
    /// <summary>
    /// Base URL of the NamFix Web API.
    /// <list type="bullet">
    /// <item>Android emulator → PC: <c>https://10.0.2.2:7111/</c> (10.0.2.2 is the emulator's alias for the host loopback).</item>
    /// <item>Physical phone → PC: <c>https://&lt;your-PC-LAN-IP&gt;:7111/</c> (same Wi-Fi; PC firewall must allow the port).</item>
    /// <item>Deployed API: the public HTTPS URL.</item>
    /// </list>
    /// The API's <c>Cors:Origins</c> allowlist does not apply here (native HTTP client, not a browser origin).
    /// </summary>
    public const string ApiBaseUrl = "https://10.0.2.2:7111/";
}
