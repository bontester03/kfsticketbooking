using KFS.Application.DTOs.Events;
using KFS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KFS.Api.Controllers;

// Pre-auth endpoints for the public landing / sign-in page. Returns only safe, aggregate data.
[ApiController]
[AllowAnonymous]
[Route("api/v1/public")]
public class PublicController : ControllerBase
{
    private readonly IEventService _events;
    public PublicController(IEventService events) => _events = events;

    [HttpGet("event")]
    public async Task<ActionResult<PublicEventDto>> ActiveEvent(CancellationToken ct)
    {
        var summary = await _events.GetPublicSummaryAsync(ct);
        return summary is null ? NoContent() : Ok(summary);
    }
}
