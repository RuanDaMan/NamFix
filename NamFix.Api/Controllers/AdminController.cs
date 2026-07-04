using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamFix.Application.Infrastructure.Mail;
using NamFix.Application.Services;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Api.Controllers;

[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminController : ApiControllerBase
{
    private readonly IAdminService _admin;
    private readonly IUserAdminService _users;
    private readonly IPlatformSettingsService _settings;
    private readonly IMailPreviewService _mailPreview;
    public AdminController(IAdminService admin, IUserAdminService users, IPlatformSettingsService settings,
        IMailPreviewService mailPreview)
    {
        _admin = admin;
        _users = users;
        _settings = settings;
        _mailPreview = mailPreview;
    }

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

    // ---- User management (with live presence / last-seen) ----

    [HttpGet("users")]
    public async Task<ActionResult<List<AdminUserDto>>> Users() =>
        Ok(await _users.ListUsersAsync());

    [HttpPost("users/{id:guid}/active")]
    public async Task<IActionResult> SetUserActive(Guid id, [FromBody] bool isActive)
    {
        await _users.SetActiveAsync(id, isActive);
        return NoContent();
    }

    [HttpPost("users/{id:guid}/role")]
    public async Task<IActionResult> SetUserRole(Guid id, UpdateUserRoleRequest request)
    {
        await _users.SetRoleAsync(id, request.Role);
        return NoContent();
    }

    [HttpPost("users/{id:guid}/password")]
    public async Task<IActionResult> ResetUserPassword(Guid id, ResetPasswordRequest request)
    {
        try
        {
            await _users.ResetPasswordAsync(id, request.NewPassword);
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("users/{id:guid}/bookings")]
    public async Task<ActionResult<List<JobRequestDto>>> UserBookings(Guid id) =>
        Ok(await _users.GetUserBookingsAsync(id));

    [HttpGet("users/{id:guid}/tickets")]
    public async Task<ActionResult<List<SupportTicketDto>>> UserTickets(Guid id) =>
        Ok(await _users.GetUserTicketsAsync(id));

    // ---- Platform settings (e.g. cancellation window) ----

    [HttpGet("settings")]
    public async Task<ActionResult<PlatformSettingsDto>> GetSettings() =>
        Ok(await _settings.GetAsync());

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(PlatformSettingsDto request)
    {
        await _settings.UpdateAsync(request);
        return NoContent();
    }

    // ---- Test emails (preview each mail type against a chosen address) ----

    [HttpGet("test-emails/types")]
    public ActionResult<IReadOnlyList<TestEmailTypeDto>> TestEmailTypes() =>
        Ok(_mailPreview.ListTypes());

    [HttpPost("test-emails")]
    public async Task<ActionResult<SendTestEmailsResultDto>> SendTestEmails(SendTestEmailsRequest request)
    {
        if (request.Keys.Count == 0)
            return BadRequest(new ErrorResponse { Error = "Select at least one email type to send." });

        return Ok(await _mailPreview.SendAsync(request.ToEmail, request.Keys));
    }
}
