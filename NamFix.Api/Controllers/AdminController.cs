using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamFix.Application.Services;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Api.Controllers;

[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminController : ApiControllerBase
{
    private readonly IAdminService _admin;
    public AdminController(IAdminService admin) => _admin = admin;

    [HttpPost("providers/{id:guid}/status")]
    public async Task<IActionResult> SetProviderStatus(Guid id, [FromBody] ProviderStatus status)
    {
        await _admin.SetProviderStatusAsync(id, status);
        return NoContent();
    }

    [HttpPost("providers/{id:guid}/verify")]
    public async Task<IActionResult> SetVerified(Guid id, [FromBody] bool verified)
    {
        await _admin.SetProviderVerifiedAsync(id, verified);
        return NoContent();
    }

    [HttpGet("commission")]
    public async Task<ActionResult<IReadOnlyList<CommissionRule>>> GetCommission() =>
        Ok(await _admin.GetCommissionRulesAsync());

    [HttpPost("commission")]
    public async Task<IActionResult> SetCommission(SetCommissionRateRequest request)
    {
        await _admin.SetCommissionRateAsync(request);
        return NoContent();
    }

    [HttpGet("revenue")]
    public async Task<ActionResult<RevenueReportDto>> Revenue([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromUtc = from ?? DateTime.UtcNow.AddMonths(-1);
        var toUtc = to ?? DateTime.UtcNow.AddDays(1);
        return Ok(await _admin.GetRevenueReportAsync(fromUtc, toUtc));
    }

    [HttpGet("tags/pending")]
    public async Task<ActionResult<IReadOnlyList<Tag>>> PendingTags() =>
        Ok(await _admin.GetPendingTagsAsync());

    [HttpPost("tags/{id:int}/moderate")]
    public async Task<IActionResult> ModerateTag(int id, [FromBody] bool approve)
    {
        await _admin.ModerateTagAsync(id, approve);
        return NoContent();
    }
}
