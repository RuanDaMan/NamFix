using Microsoft.AspNetCore.Mvc;
using NamFix.Application.Services;
using NamFix.Shared.Dtos;

namespace NamFix.Api.Controllers;

/// <summary>Public provider search backed by SQL Server full-text + filters + "near me" sorting.</summary>
public sealed class SearchController : ApiControllerBase
{
    private readonly IProviderService _providers;
    public SearchController(IProviderService providers) => _providers = providers;

    [HttpPost]
    public async Task<ActionResult<PagedResult<ProviderSearchResult>>> Search(ProviderSearchRequest request) =>
        Ok(await _providers.SearchAsync(request));
}
