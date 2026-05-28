namespace KFS.Application.Services;

public interface IStudentTicketBundleService
{
    /// <summary>One PDF containing all of the student's confirmed parent passes plus their guest
    /// ticket (if booked). Throws if the student has no confirmed booking and no guest pass.</summary>
    Task<(byte[] Bytes, string FileName)> BuildAsync(Guid studentId, CancellationToken ct = default);
}
