using KFS.Application.DTOs.Passes;
using KFS.Application.Services;
using KFS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KFS.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/passes")]
public class AdminPassesController : ControllerBase
{
    private readonly IAdminPassService _service;
    public AdminPassesController(IAdminPassService service) => _service = service;

    [HttpPost("generate")]
    public Task<GeneratePassesResponse> Generate([FromBody] GeneratePassesRequest request, CancellationToken ct)
        => _service.GenerateBatchAsync(request, ct);

    // Roster upload: 2-column XLSX (Full Name, Email). One QR per row, emailed to each holder.
    [HttpPost("from-roster")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<GenerateFromRosterResponse> FromRoster(
        [FromQuery] Guid eventId,
        [FromQuery] KFS.Domain.Enums.AdminPassType type,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new KFS.Application.Common.Exceptions.AppException("bad_input", "File is required.");
        await using var stream = file.OpenReadStream();
        return await _service.GenerateFromRosterAsync(eventId, type, stream, ct);
    }

    [HttpGet("batches")]
    public Task<IReadOnlyList<PassBatchSummaryDto>> Batches([FromQuery] Guid eventId, CancellationToken ct)
        => _service.ListBatchesAsync(eventId, ct);

    [HttpGet]
    public Task<IReadOnlyList<AdminPassDto>> List([FromQuery] Guid eventId, [FromQuery] Guid? batchId, CancellationToken ct)
        => _service.ListPassesAsync(eventId, batchId, ct);

    [HttpPatch("{id:guid}")]
    public Task<AdminPassDto> Update(Guid id, [FromBody] UpdatePassRequest request, CancellationToken ct)
        => _service.UpdateAsync(id, request, ct);

    [HttpGet("batches/{batchId:guid}/download")]
    public async Task<IActionResult> Download(Guid batchId, [FromQuery] PassOutputFormat format, CancellationToken ct)
    {
        var (bytes, contentType, fileName) = await _service.DownloadBatchAsync(batchId, format, ct);
        return File(bytes, contentType, fileName);
    }

    [HttpDelete("batches/{batchId:guid}")]
    public async Task<IActionResult> DeleteBatch(Guid batchId, CancellationToken ct)
    {
        var deleted = await _service.DeleteBatchAsync(batchId, ct);
        return Ok(new { batchId, deleted });
    }

    // Wipes every pass for the given event (optionally just one type). Filtered by ?type=VVIP|Guest|Staff|Media.
    [HttpDelete("batches")]
    public async Task<IActionResult> DeleteAll([FromQuery] Guid eventId,
        [FromQuery] KFS.Domain.Enums.AdminPassType? type, CancellationToken ct)
    {
        var deleted = await _service.DeleteAllAsync(eventId, type, ct);
        return Ok(new { eventId, type, deleted });
    }

    [HttpGet("quota")]
    public Task<IReadOnlyList<PassQuotaDto>> Quota([FromQuery] Guid eventId, CancellationToken ct)
        => _service.GetQuotasAsync(eventId, ct);

    [HttpPut("quota")]
    public Task<PassQuotaDto> SetQuota([FromBody] SetPassQuotaRequest request, CancellationToken ct)
        => _service.SetQuotaAsync(request, ct);
}
