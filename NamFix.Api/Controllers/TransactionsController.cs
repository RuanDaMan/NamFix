using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamFix.Application.Services;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Api.Controllers;

[Authorize]
public sealed class TransactionsController : ApiControllerBase
{
    private readonly ITransactionService _transactions;
    private readonly IProviderService _providers;

    public TransactionsController(ITransactionService transactions, IProviderService providers)
    {
        _transactions = transactions;
        _providers = providers;
    }

    /// <summary>Client pays a provider through the platform; commission is captured on creation.</summary>
    [Authorize(Roles = nameof(UserRole.Client))]
    [HttpPost]
    public async Task<ActionResult<TransactionDto>> Create(CreateTransactionRequest request)
    {
        try { return Ok(await _transactions.CreateAsync(CurrentUserId, request)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Release a held transaction's net payout (admin-triggered in this slice).</summary>
    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost("{id:guid}/payout")]
    public async Task<ActionResult<TransactionDto>> Payout(Guid id)
    {
        try { return Ok(await _transactions.PayoutAsync(id)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>The signed-in provider's earnings rollup.</summary>
    [Authorize(Roles = nameof(UserRole.ServiceProvider))]
    [HttpGet("earnings")]
    public async Task<ActionResult<ProviderEarningsDto>> Earnings()
    {
        var me = await _providers.GetForUserAsync(CurrentUserId);
        if (me is null) return NoContent();
        return Ok(await _transactions.GetProviderEarningsAsync(me.Id));
    }
}
