using KfsBooking.Application.DTOs.Auditoriums;
using KfsBooking.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KfsBooking.Api.Controllers;

[ApiController]
[Route("api/auditoriums")]
[Authorize]
public class AuditoriumsController : ControllerBase
{
    private readonly IAuditoriumService _service;

    public AuditoriumsController(IAuditoriumService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditoriumDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AuditoriumDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AuditoriumDto>> Create([FromBody] CreateAuditoriumRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AuditoriumDto>> Update(Guid id, [FromBody] UpdateAuditoriumRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
