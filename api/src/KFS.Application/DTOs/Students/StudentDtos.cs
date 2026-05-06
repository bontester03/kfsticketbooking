namespace KFS.Application.DTOs.Students;

public record StudentDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    DateTime DateOfBirth,
    string? GradeOrClass,
    bool IsActive,
    bool MustChangePassword,
    string? BookingStatus,
    DateTime CreatedAt);

public record StudentImportRowResultDto(int RowNumber, bool Imported, string? Message);

public record StudentImportResultDto(
    int TotalRows,
    int Imported,
    int Skipped,
    int Failed,
    IReadOnlyList<StudentImportRowResultDto> RowResults);

public record UpdateStudentRequest(bool? IsActive);

public record ResetPasswordResponseDto(string GeneratedPassword);
