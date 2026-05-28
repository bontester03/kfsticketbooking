namespace KFS.Application.DTOs.Students;

public record StudentDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    DateTime? DateOfBirth,
    string? GradeOrClass,
    bool IsActive,
    bool MustChangePassword,
    string? BookingStatus,
    DateTime CreatedAt,
    // Booked VIP seats (Confirmed bookings only), e.g. "A12 & A11"; null when no confirmed seats.
    string? BookedSeats = null,
    string? StudentNumber = null,
    string? PreferredName = null,
    string? Gender = null,
    // 1 = VIP A, 2 = VIP B, null = not yet assigned
    int? AssignedGroup = null);

public record StudentImportRowResultDto(int RowNumber, bool Imported, string? Message);

public record StudentImportResultDto(
    int TotalRows,
    int Imported,
    int Skipped,
    int Failed,
    IReadOnlyList<StudentImportRowResultDto> RowResults);

public record UpdateStudentRequest(bool? IsActive);

public record ResetPasswordResponseDto(string GeneratedPassword);

public record SendWelcomeEmailsResponseDto(int Total, int Queued);
