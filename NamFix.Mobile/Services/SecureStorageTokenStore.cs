using Microsoft.Maui.Storage;
using ITokenStore = NamFix.Shared.Contracts.ITokenStore;

namespace NamFix.Mobile.Services;

/// <summary>
/// MAUI implementation of <see cref="ITokenStore"/> backed by platform SecureStorage
/// (Android Keystore-backed EncryptedSharedPreferences). This is the mobile counterpart of the
/// web host's localStorage-backed store — nothing else in the shared UI changes.
/// </summary>
public sealed class SecureStorageTokenStore : ITokenStore
{
    private const string AccessKey = "namfix.accessToken";
    private const string RefreshKey = "namfix.refreshToken";
    private readonly ISecureStorage _storage;

    public SecureStorageTokenStore(ISecureStorage storage) => _storage = storage;

    public Task<string?> GetAccessTokenAsync() => _storage.GetAsync(AccessKey);
    public Task<string?> GetRefreshTokenAsync() => _storage.GetAsync(RefreshKey);

    public async Task SetTokensAsync(string accessToken, string refreshToken)
    {
        await _storage.SetAsync(AccessKey, accessToken);
        await _storage.SetAsync(RefreshKey, refreshToken);
    }

    public Task ClearAsync()
    {
        _storage.Remove(AccessKey);
        _storage.Remove(RefreshKey);
        return Task.CompletedTask;
    }
}
