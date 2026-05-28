using KFS.Application.DTOs.Scan;
using KFS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KFS.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/scans")]
public class AdminScansController : ControllerBase
{
    private readonly IScanAuditService _audit;
    public AdminScansController(IScanAuditService audit) => _audit = audit;

    [HttpGet]
    public Task<ScanAuditDto> Audit([FromQuery] string? search, [FromQuery] string? status, [FromQuery] string? kind, CancellationToken ct)
        => _audit.GetAuditAsync(search, status, kind, ct);
}
