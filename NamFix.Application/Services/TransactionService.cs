using NamFix.Application.Data.Repositories;
using NamFix.Shared.Contracts;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Application.Services;

public interface ITransactionService
{
    /// <summary>
    /// Create a platform-processed transaction: resolve commission, hold funds via the gateway,
    /// and record gross / commission / net payout. Money is captured into "Held" so commission is
    /// reliably secured before the provider's payout is released.
    /// </summary>
    Task<TransactionDto> CreateAsync(Guid clientUserId, CreateTransactionRequest request);

    /// <summary>Release the provider's net payout for a held transaction.</summary>
    Task<TransactionDto> PayoutAsync(Guid transactionId);

    Task<ProviderEarningsDto> GetProviderEarningsAsync(Guid providerId);
}

public sealed class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactions;
    private readonly ICommissionRepository _commission;
    private readonly IProviderRepository _providers;
    private readonly IPaymentService _payment;

    public TransactionService(
        ITransactionRepository transactions,
        ICommissionRepository commission,
        IProviderRepository providers,
        IPaymentService payment)
    {
        _transactions = transactions;
        _commission = commission;
        _providers = providers;
        _payment = payment;
    }

    public async Task<TransactionDto> CreateAsync(Guid clientUserId, CreateTransactionRequest request)
    {
        var provider = await _providers.GetByIdAsync(request.ProviderId)
            ?? throw new InvalidOperationException("Provider not found.");

        // Resolve the effective rate (provider override > category override > platform default).
        var categoryId = request.CategoryId ?? provider.PrimaryCategoryId;
        var rate = await _commission.ResolveRateAsync(provider.Id, categoryId);

        var commissionAmount = Math.Round(request.GrossAmount * rate, 2, MidpointRounding.AwayFromZero);
        var netPayout = request.GrossAmount - commissionAmount;

        // Hold the gross from the client through the payment gateway abstraction.
        var hold = await _payment.HoldAsync(request.GrossAmount, request.Currency,
            $"NamFix payment to {provider.BusinessName}");

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            ClientUserId = clientUserId,
            CategoryId = categoryId,
            GrossAmount = request.GrossAmount,
            CommissionRate = rate,
            CommissionAmount = commissionAmount,
            NetPayoutAmount = netPayout,
            Currency = request.Currency,
            PaymentReference = hold.Reference,
            Status = hold.Success ? TransactionStatus.Held : TransactionStatus.Failed,
            CreatedAtUtc = DateTime.UtcNow,
            HeldAtUtc = hold.Success ? DateTime.UtcNow : null
        };

        await _transactions.InsertAsync(transaction);
        return ToDto(transaction);
    }

    public async Task<TransactionDto> PayoutAsync(Guid transactionId)
    {
        var transaction = await _transactions.GetByIdAsync(transactionId)
            ?? throw new InvalidOperationException("Transaction not found.");

        if (transaction.Status != TransactionStatus.Held)
            throw new InvalidOperationException("Only held transactions can be paid out.");

        var payout = await _payment.PayoutAsync(
            transaction.PaymentReference ?? string.Empty, transaction.NetPayoutAmount, transaction.Currency);

        if (!payout.Success)
            throw new InvalidOperationException($"Payout failed: {payout.Error}");

        await _transactions.UpdateStatusAsync(transactionId, TransactionStatus.PaidOut, payout.Reference);
        transaction.Status = TransactionStatus.PaidOut;
        return ToDto(transaction);
    }

    public Task<ProviderEarningsDto> GetProviderEarningsAsync(Guid providerId) =>
        _transactions.GetProviderEarningsAsync(providerId);

    private static TransactionDto ToDto(Transaction t) => new()
    {
        Id = t.Id,
        ProviderId = t.ProviderId,
        ClientUserId = t.ClientUserId,
        CategoryId = t.CategoryId,
        GrossAmount = t.GrossAmount,
        CommissionRate = t.CommissionRate,
        CommissionAmount = t.CommissionAmount,
        NetPayoutAmount = t.NetPayoutAmount,
        Status = t.Status,
        Currency = t.Currency,
        PaymentReference = t.PaymentReference,
        CreatedAtUtc = t.CreatedAtUtc
    };
}
