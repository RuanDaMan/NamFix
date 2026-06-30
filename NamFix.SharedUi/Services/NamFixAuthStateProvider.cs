using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using NamFix.Shared.Contracts;

namespace NamFix.SharedUi.Services;

/// <summary>
/// Builds the Blazor authentication state from the stored JWT. Works in any host because it only
/// depends on <see cref="ITokenStore"/> (localStorage on web, SecureStorage on MAUI).
/// </summary>
public sealed class NamFixAuthStateProvider : AuthenticationStateProvider
{
    private readonly ITokenStore _tokens;
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public NamFixAuthStateProvider(ITokenStore tokens) => _tokens = tokens;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokens.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return Anonymous;

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            if (jwt.ValidTo < DateTime.UtcNow) return Anonymous;

            // Normalise role + name claims so [Authorize(Roles=...)] and context.User work in components.
            var claims = jwt.Claims.Select(c => c.Type switch
            {
                "role" => new Claim(ClaimTypes.Role, c.Value),
                "sub" => new Claim(ClaimTypes.NameIdentifier, c.Value),
                _ => c
            }).ToList();

            var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return Anonymous;
        }
    }

    /// <summary>Call after login/logout so the UI re-evaluates authorization.</summary>
    public void NotifyChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
