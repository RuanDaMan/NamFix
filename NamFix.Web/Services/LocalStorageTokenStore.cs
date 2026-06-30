using Microsoft.JSInterop;
using NamFix.Shared.Contracts;

namespace NamFix.Web.Services;

/// <summary>
/// Web implementation of <see cref="ITokenStore"/> backed by browser localStorage. A MAUI host
/// would supply a SecureStorage-backed implementation instead — nothing else changes.
/// </summary>
public sealed class LocalStorageTokenStore : ITokenStore
{
    private const string AccessKey = "namfix.accessToken";
    private const string RefreshKey = "namfix.refreshToken";
    private readonly IJSRuntime _js;

    public LocalStorageTokenStore(IJSRuntime js) => _js = js;

    public Task<string?> GetAccessTokenAsync() => GetAsync(AccessKey);
    public Task<string?> GetRefreshTokenAsync() => GetAsync(RefreshKey);

    public async Task SetTokensAsync(string accessToken, string refreshToken)
    {
        await _js.InvokeVoidAsync("localStorage.setItem", AccessKey, accessToken);
        await _js.InvokeVoidAsync("localStorage.setItem", RefreshKey, refreshToken);
    }

    public async Task ClearAsync()
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", AccessKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", RefreshKey);
    }

    private async Task<string?> GetAsync(string key)
    {
        try { return await _js.InvokeAsync<string?>("localStorage.getItem", key); }
        catch (InvalidOperationException) { return null; } // pre-render: JS not available yet
    }
}
