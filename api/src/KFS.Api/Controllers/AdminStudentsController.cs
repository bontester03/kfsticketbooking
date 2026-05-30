using ClosedXML.Excel;
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

    // Downloadable Excel template so admins know exactly which columns to fill in.
    [HttpGet("sample")]
    public IActionResult Sample()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Students");

        var headers = new[]
        {
            "Student ID", "First Name", "Last Name", "Preferred Name",
            "Email", "Gender", "Grade", "Group (VIP A / VIP B)"
        };
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0d3128");
            cell.Style.Font.FontColor = XLColor.White;
        }

        var examples = new[]
        {
            new[] { "437079", "Ahmed",  "Almudaihem", "أحمد محمد إبراهيم",  "ahmed.almudaihem@stu.kfs.sch.sa",  "Male", "Grade 12", "VIP A" },
            new[] { "433005", "Ibrahim", "Alibrahim", "إبراهيم أسامة",       "ibrahim.alibrahim@stu.kfs.sch.sa", "Male", "Grade 12", "VIP A" },
            new[] { "433098", "Talal",   "Alsaud",    "طلال بندر سعد",        "talal.alsaud@stu.kfs.sch.sa",      "Male", "Grade 12", "VIP B" }
        };
        for (var r = 0; r < examples.Length; r++)
            for (var c = 0; c < examples[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = examples[r][c];

        // Keep ID column as text so leading zeros / long numbers aren't mangled by Excel.
        ws.Column(1).Style.NumberFormat.Format = "@";
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "kfs-students-template.xlsx");
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

    // Resets every active student to their initial password and emails each one a welcome message
    // with their sign-in credentials and the booking instructions. Scoped to one event.
    [HttpPost("send-welcome-emails")]
    public Task<SendWelcomeEmailsResponseDto> SendWelcomeEmails([FromQuery] Guid eventId, CancellationToken ct)
        => _students.SendWelcomeEmailsAsync(eventId, ct);

    // Destructive: removes every student + their bookings, items, guest passes, scan logs.
    [HttpDelete]
    public async Task<IActionResult> DeleteAll(CancellationToken ct)
    {
        var deleted = await _students.DeleteAllAsync(ct);
        return Ok(new { deleted });
    }
}
