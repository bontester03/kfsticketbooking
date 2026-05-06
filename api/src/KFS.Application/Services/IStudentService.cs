using KFS.Application.DTOs.Students;

namespace KFS.Application.Services;

public interface IStudentService
{
    Task<StudentImportResultDto> ImportAsync(Stream xlsxStream, CancellationToken ct = default);
    Task<IReadOnlyList<StudentDto>> ListAsync(string? search, string? status, int skip, int take, CancellationToken ct = default);
    Task<StudentDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<StudentDto> UpdateAsync(Guid id, UpdateStudentRequest request, CancellationToken ct = default);
    Task<ResetPasswordResponseDto> ResetPasswordAsync(Guid id, CancellationToken ct = default);
}
