namespace KFS.Application.Services;

public interface IStudentTicketBundleService
{
    /// <summary>One PDF containing all of the student's confirmed parent passes plus their guest
    /// ticket (if booked). Throws if the student has no confirmed booking and no guest pass.</summary>
    Task<(byte[] Bytes, string FileName)> BuildAsync(Guid studentId, CancellationToken ct = default);

    /// <summary>Build the bundle and email it to the student. Used by the dashboard's
    /// "Email all my tickets" button — sends the same combined PDF the Download button
    /// produces, so the student gets a single inbox copy with parent + guest tickets.</summary>
    Task SendBundleEmailAsync(Guid studentId, CancellationToken ct = default);
}
