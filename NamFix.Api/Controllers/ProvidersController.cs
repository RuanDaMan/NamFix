using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamFix.Application.Services;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Api.Controllers;

public sealed class ProvidersController : ApiControllerBase
{
    private readonly IProviderService _providers;
    private readonly IReviewService _reviews;
    private readonly IAvailabilityService _availability;
    private readonly IRateCardService _rateCards;

    public ProvidersController(
        IProviderService providers,
        IReviewService reviews,
        IAvailabilityService availability,
        IRateCardService rateCards)
    {
        _providers = providers;
        _reviews = reviews;
        _availability = availability;
        _rateCards = rateCards;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProviderDto>> Get(Guid id)
    {
        var provider = await _providers.GetByIdAsync(id);
        return provider is null ? NotFound() : Ok(provider);
    }

    /// <summary>The signed-in provider's own profile (for the management dashboard).</summary>
    [Authorize(Roles = nameof(UserRole.ServiceProvider))]
    [HttpGet("me")]
    public async Task<ActionResult<ProviderDto>> GetMine()
    {
        var provider = await _providers.GetForUserAsync(CurrentUserId);
        return provider is null ? NoContent() : Ok(provider);
    }

    /// <summary>Create or update the signed-in provider's profile.</summary>
    [Authorize(Roles = nameof(UserRole.ServiceProvider))]
    [HttpPut("me")]
    public async Task<ActionResult<ProviderDto>> SaveMine(SaveProviderRequest request) =>
        Ok(await _providers.SaveAsync(CurrentUserId, request));

    [HttpGet("{id:guid}/reviews")]
    public async Task<ActionResult<IReadOnlyList<ReviewDto>>> GetReviews(Guid id) =>
        Ok(await _reviews.GetForProviderAsync(id));

    [Authorize(Roles = nameof(UserRole.Client))]
    [HttpPost("{id:guid}/reviews")]
    public async Task<ActionResult<ReviewDto>> AddReview(Guid id, CreateReviewRequest request)
    {
        if (request.ProviderId != id) return BadRequest(new { error = "Provider id mismatch." });
        try { return Ok(await _reviews.AddAsync(CurrentUserId, CurrentUserName, request)); }
        catch (ArgumentOutOfRangeException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ---- Availability calendar ----

    /// <summary>A provider's availability (weekly rules, time-off, booked slots) for the booking picker.</summary>
    [HttpGet("{id:guid}/availability")]
    public async Task<ActionResult<ProviderAvailabilityDto>> GetAvailability(Guid id) =>
        Ok(await _availability.GetForProviderAsync(id));

    [Authorize(Roles = nameof(UserRole.ServiceProvider))]
    [HttpPut("me/availability")]
    public async Task<ActionResult> SaveAvailability(SaveAvailabilityRequest request)
    {
        try { await _availability.SaveRulesAsync(CurrentUserId, request); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [Authorize(Roles = nameof(UserRole.ServiceProvider))]
    [HttpPost("me/time-off")]
    public async Task<ActionResult<TimeOffDto>> AddTimeOff(AddTimeOffRequest request)
    {
        try { return Ok(await _availability.AddTimeOffAsync(CurrentUserId, request)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [Authorize(Roles = nameof(UserRole.ServiceProvider))]
    [HttpDelete("me/time-off/{timeOffId:guid}")]
    public async Task<ActionResult> RemoveTimeOff(Guid timeOffId)
    {
        try { await _availability.RemoveTimeOffAsync(CurrentUserId, timeOffId); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ---- Rate cards ----

    [HttpGet("{id:guid}/rate-cards")]
    public async Task<ActionResult<List<RateCardDto>>> GetRateCards(Guid id) =>
        Ok(await _rateCards.ListForProviderAsync(id, activeOnly: true));

    [Authorize(Roles = nameof(UserRole.ServiceProvider))]
    [HttpGet("me/rate-cards")]
    public async Task<ActionResult<List<RateCardDto>>> GetMyRateCards() =>
        Ok(await _rateCards.ListMineAsync(CurrentUserId));

    [Authorize(Roles = nameof(UserRole.ServiceProvider))]
    [HttpPost("me/rate-cards")]
    public async Task<ActionResult<RateCardDto>> SaveRateCard(SaveRateCardRequest request)
    {
        try { return Ok(await _rateCards.SaveAsync(CurrentUserId, request)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [Authorize(Roles = nameof(UserRole.ServiceProvider))]
    [HttpDelete("me/rate-cards/{rateCardId:guid}")]
    public async Task<ActionResult> DeleteRateCard(Guid rateCardId)
    {
        try { await _rateCards.DeleteAsync(CurrentUserId, rateCardId); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
