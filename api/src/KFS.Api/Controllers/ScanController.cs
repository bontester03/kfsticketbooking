using KFS.Application.DTOs.Scan;
using KFS.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace KFS.Api.Controllers;

[ApiController]
[Route("api/v1/scan")]
public class ScanController : ControllerBase
{
    private readonly IScannerService _scanner;
    public ScanController(IScannerService scanner) => _scanner = scanner;

    [HttpPost("verify")]
    public Task<ScanResponse> Verify([FromBody] ScanRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return _scanner.VerifyAsync(request, ip, ct);
    }
}
