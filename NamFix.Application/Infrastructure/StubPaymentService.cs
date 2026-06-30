using NamFix.Shared.Contracts;

namespace NamFix.Application.Infrastructure;

/// <summary>
/// Placeholder payment gateway. It simulates a successful hold/payout/refund so the transaction
/// flow is exercisable end-to-end without a live provider. Replace with a DPO / PayToday / bank
/// EFT implementation of <see cref="IPaymentService"/> — no other layer changes.
/// </summary>
public sealed class StubPaymentService : IPaymentService
{
    public Task<PaymentResult> HoldAsync(decimal grossAmount, string currency, string description) =>
        Task.FromResult(new PaymentResult(true, $"HOLD-{Guid.NewGuid():N}"));

    public Task<PaymentResult> PayoutAsync(string paymentReference, decimal netAmount, string currency) =>
        Task.FromResult(new PaymentResult(true, $"PAYOUT-{Guid.NewGuid():N}"));

    public Task<PaymentResult> RefundAsync(string paymentReference, decimal amount, string currency) =>
        Task.FromResult(new PaymentResult(true, $"REFUND-{Guid.NewGuid():N}"));
}
