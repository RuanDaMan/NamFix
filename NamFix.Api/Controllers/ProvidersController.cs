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

    public ProvidersController(IProviderService providers, IReviewService reviews)
    {
        _providers = providers;
        _reviews = reviews;
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
}
