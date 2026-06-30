using Microsoft.AspNetCore.Mvc;
using NamFix.Application.Services;
using NamFix.Shared.Dtos;

namespace NamFix.Api.Controllers;

/// <summary>Reference data for filters and onboarding: towns, categories, approved tags.</summary>
public sealed class TaxonomyController : ApiControllerBase
{
    private readonly ITaxonomyService _taxonomy;
    public TaxonomyController(ITaxonomyService taxonomy) => _taxonomy = taxonomy;

    [HttpGet("towns")]
    public async Task<ActionResult<IReadOnlyList<TownDto>>> Towns() => Ok(await _taxonomy.GetTownsAsync());

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> Categories() => Ok(await _taxonomy.GetCategoriesAsync());

    [HttpGet("tags")]
    public async Task<ActionResult<IReadOnlyList<string>>> Tags() => Ok(await _taxonomy.GetApprovedTagsAsync());
}
