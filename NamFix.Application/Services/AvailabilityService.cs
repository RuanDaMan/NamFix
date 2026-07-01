using NamFix.Application.Data.Repositories;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;

namespace NamFix.Application.Services;

public interface IAvailabilityService
{
    /// <summary>Public availability picture for a provider: weekly rules, time-off, and booked slots.</summary>
    Task<ProviderAvailabilityDto> GetForProviderAsync(Guid providerId);
    Task SaveRulesAsync(Guid providerUserId, SaveAvailabilityRequest request);
    Task<TimeOffDto> AddTimeOffAsync(Guid providerUserId, AddTimeOffRequest request);
    Task RemoveTimeOffAsync(Guid providerUserId, Guid timeOffId);
}

/// <summary>Manages a provider's availability calendar. Writes are scoped to the caller's own profile.</summary>
public sealed class AvailabilityService : IAvailabilityService
{
    private readonly IAvailabilityRepository _availability;
    private readonly IProviderRepository _providers;
    private readonly IJobRepository _jobs;

    public AvailabilityService(IAvailabilityRepository availability, IProviderRepository providers, IJobRepository jobs)
    {
        _availability = availability;
        _providers = providers;
        _jobs = jobs;
    }

    public async Task<ProviderAvailabilityDto> GetForProviderAsync(Guid providerId)
    {
        var rules = await _availability.GetRulesAsync(providerId);
        var timeOff = await _availability.GetTimeOffAsync(providerId);
        var booked = await _jobs.ListBookedSlotsAsync(providerId);

        return new ProviderAvailabilityDto
        {
            Rules = rules.Select(r => new AvailabilityRuleDto
            {
                Id = r.Id,
                DayOfWeek = (int)r.DayOfWeek,
                StartTime = r.StartTime,
                EndTime = r.EndTime
            }).ToList(),
            TimeOff = timeOff.Select(t => new TimeOffDto
            {
                Id = t.Id,
                StartUtc = t.StartUtc,
                EndUtc = t.EndUtc,
                Reason = t.Reason
            }).ToList(),
            BookedSlots = booked
        };
    }

    public async Task SaveRulesAsync(Guid providerUserId, SaveAvailabilityRequest request)
    {
        var provider = await RequireProviderAsync(providerUserId);
        foreach (var r in request.Rules)
            if (r.EndTime <= r.StartTime)
                throw new InvalidOperationException("Each availability window must end after it starts.");

        var rules = request.Rules.Select(r => new ProviderAvailabilityRule
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            DayOfWeek = (DayOfWeek)r.DayOfWeek,
            StartTime = r.StartTime,
            EndTime = r.EndTime
        });
        await _availability.ReplaceRulesAsync(provider.Id, rules);
    }

    public async Task<TimeOffDto> AddTimeOffAsync(Guid providerUserId, AddTimeOffRequest request)
    {
        var provider = await RequireProviderAsync(providerUserId);
        if (request.EndUtc <= request.StartUtc)
            throw new InvalidOperationException("The time-off end must be after its start.");

        var timeOff = new ProviderTimeOff
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            Reason = request.Reason,
            CreatedAtUtc = DateTime.UtcNow
        };
        await _availability.AddTimeOffAsync(timeOff);
        return new TimeOffDto { Id = timeOff.Id, StartUtc = timeOff.StartUtc, EndUtc = timeOff.EndUtc, Reason = timeOff.Reason };
    }

    public async Task RemoveTimeOffAsync(Guid providerUserId, Guid timeOffId)
    {
        var provider = await RequireProviderAsync(providerUserId);
        await _availability.DeleteTimeOffAsync(provider.Id, timeOffId);
    }

    private async Task<Provider> RequireProviderAsync(Guid providerUserId) =>
        await _providers.GetByUserIdAsync(providerUserId)
        ?? throw new InvalidOperationException("You don't have a provider profile.");
}
