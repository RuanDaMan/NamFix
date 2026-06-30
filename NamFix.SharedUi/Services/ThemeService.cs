using Microsoft.JSInterop;

namespace NamFix.SharedUi.Services;

/// <summary>
/// Tracks and persists the active UI theme ("dark" or "light"). The theme is applied to the
/// document as <c>&lt;html data-theme="…"&gt;</c>, which drives the CSS token overrides in
/// <c>namfix.css</c>. An inline script in the host's index.html applies the stored theme before
/// Blazor loads (so there's no flash); this service keeps the C# state in sync and toggles at
/// runtime. The default theme is <b>dark</b>.
///
/// Host-agnostic: it only uses <see cref="IJSRuntime"/> and localStorage, which work in both the
/// Blazor WASM web host and a MAUI BlazorWebView.
/// </summary>
public sealed class ThemeService
{
    public const string Dark = "dark";
    public const string Light = "light";
    private const string StorageKey = "namfix.theme";

    private readonly IJSRuntime _js;
    private bool _initialized;

    public ThemeService(IJSRuntime js) => _js = js;

    /// <summary>The active theme ("dark" or "light"). Defaults to dark until initialized.</summary>
    public string Theme { get; private set; } = Dark;

    public bool IsDark => Theme == Dark;

    /// <summary>Raised after the theme changes so subscribed components can re-render.</summary>
    public event Action? Changed;

    /// <summary>
    /// Reads the persisted preference (defaulting to dark) and applies it. Safe to call repeatedly;
    /// only the first call does work. Call once from the layout/nav after first render.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        var stored = await GetStoredAsync();
        Theme = stored == Light ? Light : Dark; // anything unrecognised → dark default
        await ApplyAsync();
        Changed?.Invoke();
    }

    /// <summary>Flips between dark and light, persists the choice, and applies it.</summary>
    public async Task ToggleAsync() => await SetAsync(IsDark ? Light : Dark);

    public async Task SetAsync(string theme)
    {
        Theme = theme == Light ? Light : Dark;
        try { await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, Theme); }
        catch (InvalidOperationException) { /* JS not available (pre-render); applied client-side later */ }
        await ApplyAsync();
        Changed?.Invoke();
    }

    private async Task ApplyAsync()
    {
        try { await _js.InvokeVoidAsync("document.documentElement.setAttribute", "data-theme", Theme); }
        catch (InvalidOperationException) { /* JS not available yet */ }
    }

    private async Task<string?> GetStoredAsync()
    {
        try { return await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey); }
        catch (InvalidOperationException) { return null; } // pre-render: JS not available yet
    }
}
