using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamFix.Application.Services;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Api.Controllers;

/// <summary>Read-only admin view of the mailbox fetched from POP3 (populated by the inbox-sync job).</summary>
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/inbox")]
public sealed class AdminInboxController : ApiControllerBase
{
    private readonly IInboxService _inbox;
    public AdminInboxController(IInboxService inbox) => _inbox = inbox;

    [HttpGet]
    public async Task<ActionResult<List<InboxMessageDto>>> List() =>
        Ok(await _inbox.ListAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InboxMessageDetailDto>> Get(Guid id)
    {
        var message = await _inbox.GetAsync(id);
        return message is null ? NotFound() : Ok(message);
    }
}
