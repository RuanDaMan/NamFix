using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace NamFix.Api.Controllers;

/// <summary>Base controller exposing the authenticated user's id/name from JWT claims.</summary>
[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id)
            ? id
            : throw new InvalidOperationException("No authenticated user.");

    protected string CurrentUserName => User.FindFirstValue(ClaimTypes.Name) ?? "Anonymous";
}
