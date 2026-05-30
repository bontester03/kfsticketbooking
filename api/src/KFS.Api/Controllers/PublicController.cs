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

    /// <summary>List both events (Boys + Girls) for the public landing page.</summary>
    [HttpGet("events")]
    public Task<IReadOnlyList<PublicEventDto>> ListEvents(CancellationToken ct)
        => _events.ListPublicSummariesAsync(ct);

    // Legacy "active event" endpoint kept for the existing portal/admin clients —
    // returns the first available event so the landing page banner still has something.
    [HttpGet("event")]
    public async Task<ActionResult<PublicEventDto>> FirstEvent(CancellationToken ct)
    {
        var list = await _events.ListPublicSummariesAsync(ct);
        return list.Count == 0 ? NoContent() : Ok(list[0]);
    }
}
