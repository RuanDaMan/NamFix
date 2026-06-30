using NamFix.Application.Data.Repositories;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Application.Services;

/// <summary>Admin operations: provider moderation, commission config, revenue reporting, tag moderation.</summary>
public interface IAdminService
{
    Task SetProviderStatusAsync(Guid providerId, ProviderStatus status);
    Task SetProviderVerifiedAsync(Guid providerId, bool verified);
    Task SetCommissionRateAsync(SetCommissionRateRequest request);
    Task<IReadOnlyList<CommissionRule>> GetCommissionRulesAsync();
    Task<RevenueReportDto> GetRevenueReportAsync(DateTime fromUtc, DateTime toUtc);
    Task<IReadOnlyList<Tag>> GetPendingTagsAsync();
    Task ModerateTagAsync(int tagId, bool approve);
}

public sealed class AdminService : IAdminService
{
    private readonly IProviderRepository _providers;
    private readonly ICommissionRepository _commission;
    private readonly ITransactionRepository _transactions;
    private readonly ITaxonomyRepository _taxonomy;

    public AdminService(
        IProviderRepository providers,
        ICommissionRepository commission,
        ITransactionRepository transactions,
        ITaxonomyRepository taxonomy)
    {
        _providers = providers;
        _commission = commission;
        _transactions = transactions;
        _taxonomy = taxonomy;
    }

    public Task SetProviderStatusAsync(Guid providerId, ProviderStatus status) =>
        _providers.SetStatusAsync(providerId, status);

    public Task SetProviderVerifiedAsync(Guid providerId, bool verified) =>
        _providers.SetVerifiedAsync(providerId, verified);

    public Task SetCommissionRateAsync(SetCommissionRateRequest request) =>
        _commission.UpsertRuleAsync(new CommissionRule
        {
            Scope = request.Scope,
            CategoryId = request.CategoryId,
            ProviderId = request.ProviderId,
            Rate = request.Rate,
            IsActive = true
        });

    public Task<IReadOnlyList<CommissionRule>> GetCommissionRulesAsync() =>
        _commission.GetRulesAsync();

    public Task<RevenueReportDto> GetRevenueReportAsync(DateTime fromUtc, DateTime toUtc) =>
        _transactions.GetRevenueReportAsync(fromUtc, toUtc);

    public Task<IReadOnlyList<Tag>> GetPendingTagsAsync() =>
        _taxonomy.GetTagsAsync(TagStatus.Pending);

    public Task ModerateTagAsync(int tagId, bool approve) =>
        _taxonomy.SetTagStatusAsync(tagId, approve ? TagStatus.Approved : TagStatus.Rejected);
}
