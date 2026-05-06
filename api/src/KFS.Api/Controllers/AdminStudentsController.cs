using KFS.Application.DTOs.Students;
using KFS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KFS.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/students")]
public class AdminStudentsController : ControllerBase
{
    private readonly IStudentService _students;
    public AdminStudentsController(IStudentService students) => _students = students;

    [HttpPost("upload")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<StudentImportResultDto> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new KFS.Application.Common.Exceptions.AppException("bad_input", "File is required.");
        await using var stream = file.OpenReadStream();
        return await _students.ImportAsync(stream, ct);
    }

    [HttpGet]
    public Task<IReadOnlyList<StudentDto>> List([FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        => _students.ListAsync(search, status, skip, take, ct);

    [HttpGet("{id:guid}")]
    public Task<StudentDto> Get(Guid id, CancellationToken ct) => _students.GetAsync(id, ct);

    [HttpPatch("{id:guid}")]
    public Task<StudentDto> Update(Guid id, [FromBody] UpdateStudentRequest request, CancellationToken ct)
        => _students.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/reset-password")]
    public Task<ResetPasswordResponseDto> ResetPassword(Guid id, CancellationToken ct)
        => _students.ResetPasswordAsync(id, ct);
}
