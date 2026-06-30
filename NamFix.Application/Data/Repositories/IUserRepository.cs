using Dapper;
using NamFix.Shared.Domain;

namespace NamFix.Application.Data.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(Guid id);
    Task InsertAsync(User user);
    Task AddRefreshTokenAsync(RefreshToken token);
    Task<RefreshToken?> GetRefreshTokenAsync(string token);
    Task RevokeRefreshTokenAsync(Guid id);
}

public sealed class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _db;
    public UserRepository(IDbConnectionFactory db) => _db = db;

    public async Task<User?> GetByEmailAsync(string email)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM dbo.Users WHERE Email = @email", new { email });
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM dbo.Users WHERE Id = @id", new { id });
    }

    public async Task InsertAsync(User user)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.Users (Id, Email, PhoneNumber, FullName, Role, PasswordHash, IsActive, CreatedAtUtc)
            VALUES (@Id, @Email, @PhoneNumber, @FullName, @Role, @PasswordHash, @IsActive, @CreatedAtUtc)
            """, user);
    }

    public async Task AddRefreshTokenAsync(RefreshToken token)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.RefreshTokens (Id, UserId, Token, ExpiresAtUtc, CreatedAtUtc, RevokedAtUtc)
            VALUES (@Id, @UserId, @Token, @ExpiresAtUtc, @CreatedAtUtc, @RevokedAtUtc)
            """, token);
    }

    public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<RefreshToken>(
            "SELECT * FROM dbo.RefreshTokens WHERE Token = @token", new { token });
    }

    public async Task RevokeRefreshTokenAsync(Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.RefreshTokens SET RevokedAtUtc = SYSUTCDATETIME() WHERE Id = @id", new { id });
    }
}
