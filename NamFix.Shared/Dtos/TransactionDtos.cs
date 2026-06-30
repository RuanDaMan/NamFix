using System.ComponentModel.DataAnnotations;
using NamFix.Shared.Enums;

namespace NamFix.Shared.Dtos;

/// <summary>Client initiates a platform-processed payment to a provider.</summary>
public record CreateTransactionRequest
{
    [Required]
    public Guid ProviderId { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal GrossAmount { get; init; }

    public int? CategoryId { get; init; }
    public string Currency { get; init; } = "NAD";
}

public record TransactionDto
{
    public Guid Id { get; init; }
    public Guid ProviderId { get; init; }
    public Guid ClientUserId { get; init; }
    public int? CategoryId { get; init; }
    public decimal GrossAmount { get; init; }
    public decimal CommissionRate { get; init; }
    public decimal CommissionAmount { get; init; }
    public decimal NetPayoutAmount { get; init; }
    public TransactionStatus Status { get; init; }
    public string Currency { get; init; } = "NAD";
    public string? PaymentReference { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

/// <summary>Provider's earnings rollup: gross billed, commission deducted, net payout.</summary>
public record ProviderEarningsDto
{
    public decimal TotalGross { get; init; }
    public decimal TotalCommission { get; init; }
    public decimal TotalNetPayout { get; init; }
    public decimal PendingPayout { get; init; }
    public int TransactionCount { get; init; }
}

/// <summary>Admin revenue rollup over a period, sliceable by town/category/provider.</summary>
public record RevenueReportDto
{
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc { get; init; }
    public decimal TotalGross { get; init; }
    public decimal TotalCommission { get; init; }
    public int TransactionCount { get; init; }
    public List<RevenueBreakdownRow> ByCategory { get; init; } = new();
    public List<RevenueBreakdownRow> ByTown { get; init; } = new();
}

public record RevenueBreakdownRow
{
    public string Label { get; init; } = string.Empty;
    public decimal Commission { get; init; }
    public decimal Gross { get; init; }
    public int Count { get; init; }
}

public record SetCommissionRateRequest
{
    public CommissionScope Scope { get; init; }
    public int? CategoryId { get; init; }
    public Guid? ProviderId { get; init; }

    [Range(0, 1)]
    public decimal Rate { get; init; }
}
