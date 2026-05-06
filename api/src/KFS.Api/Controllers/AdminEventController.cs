using KFS.Application.DTOs.Events;
using KFS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KFS.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/event")]
public class AdminEventController : ControllerBase
{
    private readonly IEventService _events;
    public AdminEventController(IEventService events) => _events = events;

    [HttpGet]
    public Task<EventDto> Get(CancellationToken ct) => _events.GetActiveAsync(ct);

    [HttpPut("{eventId:guid}")]
    public Task<EventDto> Update(Guid eventId, [FromBody] UpdateEventRequest request, CancellationToken ct)
        => _events.UpdateAsync(eventId, request, ct);
}
