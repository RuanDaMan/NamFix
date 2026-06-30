using System.Net.Http.Headers;
using NamFix.Shared.Contracts;

namespace NamFix.SharedUi.Services;

/// <summary>
/// Attaches the stored JWT as a Bearer header on outgoing API calls. Registered on the API
/// HttpClient by the host, so components never deal with tokens directly.
/// </summary>
public sealed class AuthHeaderHandler : DelegatingHandler
{
    private readonly ITokenStore _tokens;
    public AuthHeaderHandler(ITokenStore tokens) => _tokens = tokens;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await _tokens.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, ct);
    }
}
