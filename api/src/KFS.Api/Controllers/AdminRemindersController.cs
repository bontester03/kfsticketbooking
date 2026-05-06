using KFS.Application.DTOs.Reminders;
using KFS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KFS.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/reminders")]
public class AdminRemindersController : ControllerBase
{
    private readonly IReminderService _service;
    public AdminRemindersController(IReminderService service) => _service = service;

    [HttpPost("unbooked")]
    public async Task<object> SendUnbooked([FromBody] SendUnbookedReminderRequest request, CancellationToken ct)
    {
        var sent = await _service.SendUnbookedAsync(request, ct);
        return new { sent };
    }

    [HttpGet("logs")]
    public Task<IReadOnlyList<ReminderLogDto>> Logs([FromQuery] int take = 100, CancellationToken ct = default)
        => _service.ListLogsAsync(take, ct);
}
