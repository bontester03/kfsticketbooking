using KFS.Application.DTOs.Events;
using KFS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KFS.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/events")]
public class AdminEventController : ControllerBase
{
    private readonly IEventService _events;
    public AdminEventController(IEventService events) => _events = events;

    // Event picker — both events for the admin landing page.
    [HttpGet]
    public Task<IReadOnlyList<EventDto>> List(CancellationToken ct) => _events.ListAsync(ct);

    [HttpGet("{eventId:guid}")]
    public Task<EventDto> Get(Guid eventId, CancellationToken ct) => _events.GetByIdAsync(eventId, ct);

    [HttpGet("by-slug/{slug}")]
    public Task<EventDto> GetBySlug(string slug, CancellationToken ct) => _events.GetBySlugAsync(slug, ct);

    [HttpPut("{eventId:guid}")]
    public Task<EventDto> Update(Guid eventId, [FromBody] UpdateEventRequest request, CancellationToken ct)
        => _events.UpdateAsync(eventId, request, ct);
}
