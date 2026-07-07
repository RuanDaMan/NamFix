using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace NamFix.SharedUi.Services;

/// <summary>
/// Keeps an in-app history stack so the mobile hardware back button can return to the previous screen.
/// Blazor Hybrid's WebView doesn't reliably honour <c>window.history.back()</c> (the router isn't driven
/// by the WebView's own history), so we track navigations ourselves and go back via
/// <see cref="NavigationManager"/>, which always re-renders the router.
/// </summary>
public sealed class NavigationHistory : IDisposable
{
    private readonly NavigationManager _nav;
    private readonly List<string> _stack = new();
    private bool _navigatingBack;

    /// <summary>Raised whenever the stack changes, so the host can push <see cref="CanGoBack"/> to JS.</summary>
    public event Action? Changed;

    public NavigationHistory(NavigationManager nav)
    {
        _nav = nav;
        _stack.Add(_nav.Uri);
        _nav.LocationChanged += OnLocationChanged;
    }

    /// <summary>True when there is a previous screen to return to.</summary>
    public bool CanGoBack => _stack.Count > 1;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (_navigatingBack)
        {
            // This LocationChanged is the programmatic back-navigation we just triggered — don't re-push.
            _navigatingBack = false;
        }
        else if (_stack.Count == 0 || !string.Equals(_stack[^1], e.Location, StringComparison.Ordinal))
        {
            _stack.Add(e.Location);
        }

        Changed?.Invoke();
    }

    /// <summary>Navigate to the previous screen. Returns false when there is nowhere to go back to.</summary>
    public bool GoBack()
    {
        if (_stack.Count <= 1) return false;
        _stack.RemoveAt(_stack.Count - 1);   // drop the current entry
        var target = _stack[^1];             // the previous entry becomes current
        _navigatingBack = true;
        _nav.NavigateTo(target);
        return true;
    }

    public void Dispose() => _nav.LocationChanged -= OnLocationChanged;
}
