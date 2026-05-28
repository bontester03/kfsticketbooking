using KFS.Application.DTOs.Passes;

namespace KFS.Application.Services;

public interface IGuestPassService
{
    /// <summary>Books the one Guest ticket (1 QR, admits 3) for a child. issuedByAdminId is set
    /// when an admin issues it on the child's behalf; null when the student self-books.</summary>
    Task<GuestPassDto> BookForStudentAsync(Guid studentId, Guid? issuedByAdminId, string? issuedToName, CancellationToken ct = default);

    /// <summary>The child's guest ticket (with live scan status), or null if they have none.</summary>
    Task<GuestPassDto?> GetForStudentAsync(Guid studentId, CancellationToken ct = default);

    Task<GuestAnalyticsDto> GetAnalyticsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<GuestEligibleStudentDto>> ListStudentsAsync(string? search, CancellationToken ct = default);
}
