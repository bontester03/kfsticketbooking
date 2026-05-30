using KFS.Application.DTOs.Students;

namespace KFS.Application.Services;

public interface IStudentService
{
    /// <summary>Scoped to one event. Every row whose Gender doesn't match the target
    /// event is rejected — no silent cross-event imports.</summary>
    Task<StudentImportResultDto> ImportAsync(Guid eventId, Stream xlsxStream, CancellationToken ct = default);
    Task<IReadOnlyList<StudentDto>> ListAsync(string? search, string? status, int skip, int take, CancellationToken ct = default);
    Task<StudentDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<StudentDto> UpdateAsync(Guid id, UpdateStudentRequest request, CancellationToken ct = default);
    Task<ResetPasswordResponseDto> ResetPasswordAsync(Guid id, CancellationToken ct = default);

    /// <summary>Wipes every student and everything that hangs off them — bookings, booking items,
    /// student-linked guest passes, password resets — plus the scan logs for those tickets.
    /// Returns the number of students removed.</summary>
    Task<int> DeleteAllAsync(CancellationToken ct = default);

    /// <summary>Resets every active student of THIS event to their initial password and (in the background)
    /// emails each one a welcome message with sign-in details. Returns how many were queued.</summary>
    Task<SendWelcomeEmailsResponseDto> SendWelcomeEmailsAsync(Guid eventId, CancellationToken ct = default);
}
