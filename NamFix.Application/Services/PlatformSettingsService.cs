using NamFix.Application.Data.Repositories;
using NamFix.Shared.Dtos;

namespace NamFix.Application.Services;

public interface IPlatformSettingsService
{
    Task<PlatformSettingsDto> GetAsync();
    Task UpdateAsync(PlatformSettingsDto dto);
    Task<int> GetFreeCancellationWindowHoursAsync();
}

/// <summary>Reads/writes the admin-editable platform tunables (key/value store).</summary>
public sealed class PlatformSettingsService : IPlatformSettingsService
{
    private const string FreeCancellationWindowHours = "FreeCancellationWindowHours";
    private readonly IPlatformSettingsRepository _repo;

    public PlatformSettingsService(IPlatformSettingsRepository repo) => _repo = repo;

    public async Task<PlatformSettingsDto> GetAsync()
    {
        var all = await _repo.GetAllAsync();
        return new PlatformSettingsDto
        {
            FreeCancellationWindowHours = GetInt(all, FreeCancellationWindowHours, 24)
        };
    }

    public Task UpdateAsync(PlatformSettingsDto dto) =>
        _repo.SetAsync(FreeCancellationWindowHours, dto.FreeCancellationWindowHours.ToString());

    public async Task<int> GetFreeCancellationWindowHoursAsync()
    {
        var all = await _repo.GetAllAsync();
        return GetInt(all, FreeCancellationWindowHours, 24);
    }

    private static int GetInt(IReadOnlyDictionary<string, string> map, string key, int fallback) =>
        map.TryGetValue(key, out var raw) && int.TryParse(raw, out var v) ? v : fallback;
}
