using NamFix.Application.Data.Repositories;
using NamFix.Application.Security;
using NamFix.Shared.Contracts;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Application.Services;

/// <summary>
/// Admin user-management operations: listing users (with live presence + last-seen), activating /
/// deactivating accounts, changing roles, resetting passwords, and drilling into a user's bookings
/// and support tickets. Presence is read from <see cref="IPresenceTracker"/>, which the realtime hub
/// keeps current.
/// </summary>
public interface IUserAdminService
{
    Task<List<AdminUserDto>> ListUsersAsync();
    Task SetActiveAsync(Guid userId, bool isActive);
    Task SetRoleAsync(Guid userId, UserRole role);
    Task ResetPasswordAsync(Guid userId, string newPassword);
    Task<List<JobRequestDto>> GetUserBookingsAsync(Guid userId);
    Task<List<SupportTicketDto>> GetUserTicketsAsync(Guid userId);
}

public sealed class UserAdminService : IUserAdminService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPresenceTracker _presence;
    private readonly IJobRepository _jobs;
    private readonly ISupportRepository _support;

    public UserAdminService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IPresenceTracker presence,
        IJobRepository jobs,
        ISupportRepository support)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _presence = presence;
        _jobs = jobs;
        _support = support;
    }

    public async Task<List<AdminUserDto>> ListUsersAsync()
    {
        var users = await _users.ListAdminUsersAsync();
        // Overlay live presence from the realtime hub onto the persisted snapshot.
        return users.Select(u => u with { IsOnline = _presence.IsOnline(u.Id) }).ToList();
    }

    public Task SetActiveAsync(Guid userId, bool isActive) => _users.SetActiveAsync(userId, isActive);

    public Task SetRoleAsync(Guid userId, UserRole role) => _users.SetRoleAsync(userId, role);

    public async Task ResetPasswordAsync(Guid userId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            throw new InvalidOperationException("Password must be at least 8 characters.");
        var user = await _users.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");
        await _users.UpdatePasswordHashAsync(user.Id, _passwordHasher.Hash(newPassword));
    }

    public Task<List<JobRequestDto>> GetUserBookingsAsync(Guid userId) =>
        _jobs.ListDtosForUserAsync(userId);

    public Task<List<SupportTicketDto>> GetUserTicketsAsync(Guid userId) =>
        _support.ListTicketDtosForUserAsync(userId);
}
