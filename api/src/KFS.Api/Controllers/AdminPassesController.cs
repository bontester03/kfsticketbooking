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

    [HttpGet("batches")]
    public Task<IReadOnlyList<PassBatchSummaryDto>> Batches(CancellationToken ct) => _service.ListBatchesAsync(ct);

    [HttpGet]
    public Task<IReadOnlyList<AdminPassDto>> List([FromQuery] Guid? batchId, CancellationToken ct)
        => _service.ListPassesAsync(batchId, ct);

    [HttpPatch("{id:guid}")]
    public Task<AdminPassDto> Update(Guid id, [FromBody] UpdatePassRequest request, CancellationToken ct)
        => _service.UpdateAsync(id, request, ct);

    [HttpGet("batches/{batchId:guid}/download")]
    public async Task<IActionResult> Download(Guid batchId, [FromQuery] PassOutputFormat format, CancellationToken ct)
    {
        var (bytes, contentType, fileName) = await _service.DownloadBatchAsync(batchId, format, ct);
        return File(bytes, contentType, fileName);
    }
}
