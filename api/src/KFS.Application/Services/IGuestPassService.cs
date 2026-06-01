using KFS.Application.DTOs.Passes;

namespace KFS.Application.Services;

public interface IGuestPassService
{
    /// <summary>Books the one Guest ticket (1 QR, admits 3) for a child. issuedByAdminId is set
    /// when an admin issues it on the child's behalf; null when the student self-books.</summary>
    Task<GuestPassDto> BookForStudentAsync(Guid studentId, Guid? issuedByAdminId, string? issuedToName, CancellationToken ct = default);

    /// <summary>The child's guest ticket (with live scan status), or null if they have none.
    /// Also re-renders the QR PNG file on every call so a Railway redeploy without a
    /// persistent volume mounted can't leave the student staring at a broken image.</summary>
    Task<GuestPassDto?> GetForStudentAsync(Guid studentId, CancellationToken ct = default);

    /// <summary>Student-initiated cancel of their own guest ticket. Blocks once the QR
    /// has been admitted at the gate (any non-zero valid scans). Use BookForStudentAsync
    /// afterwards to issue a fresh one.</summary>
    Task CancelForStudentAsync(Guid studentId, CancellationToken ct = default);

    Task<GuestAnalyticsDto> GetAnalyticsAsync(Guid eventId, CancellationToken ct = default);

    Task<IReadOnlyList<GuestEligibleStudentDto>> ListStudentsAsync(Guid eventId, string? search, CancellationToken ct = default);
}
