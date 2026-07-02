using Dapper;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Application.Data.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(Guid id);
    Task InsertAsync(User user);
    Task AddRefreshTokenAsync(RefreshToken token);
    Task<RefreshToken?> GetRefreshTokenAsync(string token);
    Task RevokeRefreshTokenAsync(Guid id);

    /// <summary>Revoke every active refresh token for a user (e.g. after a password reset).</summary>
    Task RevokeAllRefreshTokensForUserAsync(Guid userId);

    // ---- Password reset ----
    Task AddPasswordResetTokenAsync(PasswordResetToken token);
    Task<PasswordResetToken?> GetPasswordResetTokenAsync(string token);
    Task MarkPasswordResetTokenUsedAsync(Guid id);

    // ---- Admin user management + presence ----

    /// <summary>All users projected for the admin page (with booking/ticket counts). IsOnline is filled in by the service.</summary>
    Task<List<AdminUserDto>> ListAdminUsersAsync();

    /// <summary>Ids of all Admin-role users — used to fan out ticket notifications to the support team.</summary>
    Task<List<Guid>> ListAdminIdsAsync();

    Task SetActiveAsync(Guid id, bool isActive);
    Task SetRoleAsync(Guid id, UserRole role);
    Task UpdatePasswordHashAsync(Guid id, string passwordHash);
    Task UpdateLastSeenAsync(Guid id, DateTime utc);
    Task IncrementNoShowAsync(Guid id);
    Task IncrementLateCancellationAsync(Guid id);
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

    public async Task RevokeAllRefreshTokensForUserAsync(Guid userId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.RefreshTokens SET RevokedAtUtc = SYSUTCDATETIME() WHERE UserId = @userId AND RevokedAtUtc IS NULL",
            new { userId });
    }

    public async Task AddPasswordResetTokenAsync(PasswordResetToken token)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.PasswordResetTokens (Id, UserId, Token, ExpiresAtUtc, CreatedAtUtc, UsedAtUtc)
            VALUES (@Id, @UserId, @Token, @ExpiresAtUtc, @CreatedAtUtc, @UsedAtUtc)
            """, token);
    }

    public async Task<PasswordResetToken?> GetPasswordResetTokenAsync(string token)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<PasswordResetToken>(
            "SELECT * FROM dbo.PasswordResetTokens WHERE Token = @token", new { token });
    }

    public async Task MarkPasswordResetTokenUsedAsync(Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.PasswordResetTokens SET UsedAtUtc = SYSUTCDATETIME() WHERE Id = @id", new { id });
    }

    public async Task<List<AdminUserDto>> ListAdminUsersAsync()
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<AdminUserDto>(
            """
            SELECT u.Id, u.Email, u.FullName, u.PhoneNumber, u.Role, u.IsActive, u.CreatedAtUtc, u.LastSeenUtc,
                   (SELECT COUNT(*) FROM dbo.JobRequests j WHERE j.ClientUserId = u.Id OR j.ProviderUserId = u.Id) AS BookingCount,
                   (SELECT COUNT(*) FROM dbo.SupportTickets t WHERE t.RequesterUserId = u.Id) AS TicketCount
            FROM dbo.Users u
            ORDER BY u.CreatedAtUtc DESC
            """)).AsList();
    }

    public async Task<List<Guid>> ListAdminIdsAsync()
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<Guid>(
            "SELECT Id FROM dbo.Users WHERE Role = @role AND IsActive = 1",
            new { role = (int)UserRole.Admin })).AsList();
    }

    public async Task SetActiveAsync(Guid id, bool isActive)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.Users SET IsActive = @isActive WHERE Id = @id", new { id, isActive });
    }

    public async Task SetRoleAsync(Guid id, UserRole role)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.Users SET Role = @role WHERE Id = @id", new { id, role = (int)role });
    }

    public async Task UpdatePasswordHashAsync(Guid id, string passwordHash)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.Users SET PasswordHash = @passwordHash WHERE Id = @id", new { id, passwordHash });
    }

    public async Task UpdateLastSeenAsync(Guid id, DateTime utc)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.Users SET LastSeenUtc = @utc WHERE Id = @id", new { id, utc });
    }

    public async Task IncrementNoShowAsync(Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.Users SET NoShowCount = NoShowCount + 1 WHERE Id = @id", new { id });
    }

    public async Task IncrementLateCancellationAsync(Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.Users SET LateCancellationCount = LateCancellationCount + 1 WHERE Id = @id", new { id });
    }
}
