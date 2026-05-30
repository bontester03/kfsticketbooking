using KFS.Application.DTOs.Scan;

namespace KFS.Application.Services;

public interface IScanAuditService
{
    /// <summary>Every issued ticket (admin passes + student seats) FOR ONE EVENT, with whether/when it was scanned.
    /// status: "scanned" | "unscanned" | null (all). kind: VVIP|Guest|Staff|Media|Seat | null (all).</summary>
    Task<ScanAuditDto> GetAuditAsync(Guid eventId, string? search, string? status, string? kind, CancellationToken ct = default);
}
