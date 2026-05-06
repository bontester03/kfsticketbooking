using KFS.Application.DTOs.Scan;

namespace KFS.Application.Services;

public interface IScannerService
{
    Task<ScanResponse> VerifyAsync(ScanRequest request, string? scannerIp, CancellationToken ct = default);
}
