using NamFix.Application.Data.Repositories;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;

namespace NamFix.Application.Services;

public interface IReviewService
{
    Task<IReadOnlyList<ReviewDto>> GetForProviderAsync(Guid providerId);
    Task<ReviewDto> AddAsync(Guid clientUserId, string clientName, CreateReviewRequest request);
}

public sealed class ReviewService : IReviewService
{
    private readonly IReviewRepository _reviews;
    private readonly IProviderRepository _providers;

    public ReviewService(IReviewRepository reviews, IProviderRepository providers)
    {
        _reviews = reviews;
        _providers = providers;
    }

    public Task<IReadOnlyList<ReviewDto>> GetForProviderAsync(Guid providerId) =>
        _reviews.GetForProviderAsync(providerId);

    public async Task<ReviewDto> AddAsync(Guid clientUserId, string clientName, CreateReviewRequest request)
    {
        if (request.Rating is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(request), "Rating must be between 1 and 5.");

        // Verified-review flag is set when the contact/transaction happened on-platform.
        var isVerified = await _reviews.HasCompletedTransactionAsync(request.ProviderId, clientUserId);

        var review = new Review
        {
            Id = Guid.NewGuid(),
            ProviderId = request.ProviderId,
            ClientUserId = clientUserId,
            Rating = request.Rating,
            Comment = request.Comment,
            IsVerified = isVerified,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _reviews.InsertAsync(review);
        await _providers.RecalculateRatingAsync(request.ProviderId);

        return new ReviewDto
        {
            Id = review.Id,
            ProviderId = review.ProviderId,
            ClientName = clientName,
            Rating = review.Rating,
            Comment = review.Comment,
            IsVerified = review.IsVerified,
            CreatedAtUtc = review.CreatedAtUtc
        };
    }
}
