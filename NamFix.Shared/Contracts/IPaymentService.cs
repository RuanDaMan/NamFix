namespace NamFix.Shared.Contracts;

/// <summary>
/// Payment gateway abstraction. A local provider (DPO / PayToday / bank EFT) wires in behind this
/// interface later. Designed around a "hold then release/payout" pattern so the platform can
/// reliably capture commission before releasing the provider's net payout.
/// </summary>
public interface IPaymentService
{
    /// <summary>Authorize and hold the gross amount from the client. Returns a gateway reference.</summary>
    Task<PaymentResult> HoldAsync(decimal grossAmount, string currency, string description);

    /// <summary>Release the provider's net payout after commission has been captured.</summary>
    Task<PaymentResult> PayoutAsync(string paymentReference, decimal netAmount, string currency);

    /// <summary>Reverse a held or captured payment.</summary>
    Task<PaymentResult> RefundAsync(string paymentReference, decimal amount, string currency);
}

public record PaymentResult(bool Success, string? Reference, string? Error = null);
