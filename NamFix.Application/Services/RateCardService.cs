using NamFix.Application.Data.Repositories;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;

namespace NamFix.Application.Services;

public interface IRateCardService
{
    Task<List<RateCardDto>> ListForProviderAsync(Guid providerId, bool activeOnly);
    Task<List<RateCardDto>> ListMineAsync(Guid providerUserId);
    Task<RateCardDto> SaveAsync(Guid providerUserId, SaveRateCardRequest request);
    Task DeleteAsync(Guid providerUserId, Guid id);
}

/// <summary>Manages provider rate cards and keeps the denormalized Providers.StartingPrice in sync.</summary>
public sealed class RateCardService : IRateCardService
{
    private readonly IRateCardRepository _rateCards;
    private readonly IProviderRepository _providers;

    public RateCardService(IRateCardRepository rateCards, IProviderRepository providers)
    {
        _rateCards = rateCards;
        _providers = providers;
    }

    public Task<List<RateCardDto>> ListForProviderAsync(Guid providerId, bool activeOnly) =>
        _rateCards.ListDtosForProviderAsync(providerId, activeOnly);

    public async Task<List<RateCardDto>> ListMineAsync(Guid providerUserId)
    {
        var provider = await RequireProviderAsync(providerUserId);
        return await _rateCards.ListDtosForProviderAsync(provider.Id, activeOnly: false);
    }

    public async Task<RateCardDto> SaveAsync(Guid providerUserId, SaveRateCardRequest request)
    {
        var provider = await RequireProviderAsync(providerUserId);
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Enter a title for the rate.");
        if (request.Price <= 0)
            throw new InvalidOperationException("Enter a price greater than zero.");

        // If editing, make sure the row belongs to the caller.
        if (request.Id is { } id)
        {
            var existing = await _rateCards.GetByIdAsync(id);
            if (existing is null || existing.ProviderId != provider.Id)
                throw new InvalidOperationException("Rate card not found.");
        }

        var card = new ProviderRateCard
        {
            Id = request.Id ?? Guid.NewGuid(),
            ProviderId = provider.Id,
            CategoryId = request.CategoryId,
            Title = request.Title.Trim(),
            Description = request.Description,
            Price = request.Price,
            Unit = request.Unit,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder
        };
        await _rateCards.UpsertAsync(card);
        await SyncStartingPriceAsync(provider.Id);

        return (await _rateCards.ListDtosForProviderAsync(provider.Id, activeOnly: false))
            .First(x => x.Id == card.Id);
    }

    public async Task DeleteAsync(Guid providerUserId, Guid id)
    {
        var provider = await RequireProviderAsync(providerUserId);
        await _rateCards.DeleteAsync(provider.Id, id);
        await SyncStartingPriceAsync(provider.Id);
    }

    private async Task SyncStartingPriceAsync(Guid providerId) =>
        await _providers.SetStartingPriceAsync(providerId, await _rateCards.GetMinActivePriceAsync(providerId));

    private async Task<Provider> RequireProviderAsync(Guid providerUserId) =>
        await _providers.GetByUserIdAsync(providerUserId)
        ?? throw new InvalidOperationException("You don't have a provider profile.");
}
