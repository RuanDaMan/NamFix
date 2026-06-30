using NamFix.Application.Data.Repositories;
using NamFix.Application.Security;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;

namespace NamFix.Application.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshAsync(string refreshToken);
}

/// <summary>Thrown for auth failures so the API can translate to 400/401 without leaking detail.</summary>
public sealed class AuthException(string message) : Exception(message);

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly JwtOptions _jwtOptions;

    public AuthService(IUserRepository users, IPasswordHasher hasher, IJwtTokenService jwt, JwtOptions jwtOptions)
    {
        _users = users;
        _hasher = hasher;
        _jwt = jwt;
        _jwtOptions = jwtOptions;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existing = await _users.GetByEmailAsync(request.Email);
        if (existing is not null)
            throw new AuthException("An account with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PhoneNumber = request.PhoneNumber,
            FullName = request.FullName.Trim(),
            Role = request.Role,
            PasswordHash = _hasher.Hash(request.Password),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await _users.InsertAsync(user);
        return await IssueTokensAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is null || !user.IsActive || !_hasher.Verify(request.Password, user.PasswordHash))
            throw new AuthException("Invalid email or password.");

        return await IssueTokensAsync(user);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken)
    {
        var stored = await _users.GetRefreshTokenAsync(refreshToken);
        if (stored is null || !stored.IsActive)
            throw new AuthException("Invalid or expired refresh token.");

        var user = await _users.GetByIdAsync(stored.UserId)
            ?? throw new AuthException("User no longer exists.");

        // Rotate: revoke the used token and issue a fresh pair.
        await _users.RevokeRefreshTokenAsync(stored.Id);
        return await IssueTokensAsync(user);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user)
    {
        var (accessToken, expiresAt) = _jwt.CreateAccessToken(user);
        var refresh = _jwt.CreateRefreshToken();

        await _users.AddRefreshTokenAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refresh,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays)
        });

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refresh,
            ExpiresAtUtc = expiresAt,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role
            }
        };
    }
}
