using ClosedXML.Excel;
using KFS.Application.DTOs.Students;
using KFS.Application.Services;
using KFS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KFS.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/students")]
public class AdminStudentsController : ControllerBase
{
    private readonly IStudentService _students;
    private readonly IEventService _events;
    public AdminStudentsController(IStudentService students, IEventService events)
    {
        _students = students;
        _events = events;
    }

    // Scoped to one event — rows whose Gender doesn't match get rejected with a per-row
    // error. Prevents silent cross-event imports (an XLSX for Girls uploaded on the Boys
    // page no longer "succeeds").
    [HttpPost("upload")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<StudentImportResultDto> Upload([FromQuery] Guid eventId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new KFS.Application.Common.Exceptions.AppException("bad_input", "File is required.");
        await using var stream = file.OpenReadStream();
        return await _students.ImportAsync(eventId, stream, ct);
    }

    // Downloadable Excel template so admins know exactly which columns to fill in.
    [HttpGet("sample")]
    public async Task<IActionResult> Sample([FromQuery] Guid? eventId, CancellationToken ct)
    {
        var gender = EventGender.Male;
        if (eventId.HasValue)
        {
            var ev = await _events.GetByIdAsync(eventId.Value, ct);
            gender = ev.Gender;
        }
        var isBoys = gender == EventGender.Male;
        var genderText = isBoys ? "Male" : "Female";
        var genderCode = isBoys ? "1" : "2";
        var genderWord = isBoys ? "Boys" : "Girls";

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Students");

        var headers = new[]
        {
            "Student ID", "First Name", "Last Name", "Preferred Name",
            "Email", "Gender (Male/Female or 1=Boys, 2=Girls)", "Grade",
            "Group (VIP A/VIP B or 1=A, 2=B)"
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
        examples = isBoys
            ? new[]
            {
                new[] { "437079", "Ahmed",  "Almudaihem", "", "ahmed.almudaihem@stu.kfs.sch.sa", genderText, "Grade 12", "VIP A" },
                new[] { "433005", "Ibrahim", "Alibrahim", "", "ibrahim.alibrahim@stu.kfs.sch.sa", genderCode, "Grade 12", "1" },
                new[] { "433098", "Talal",   "Alsaud",    "", "talal.alsaud@stu.kfs.sch.sa", genderWord, "Grade 12", "2" }
            }
            : new[]
            {
                new[] { "537079", "Safa",  "Albuhairan", "", "safa.albuhairan@stu.kfs.sch.sa", genderText, "Grade 12", "VIP A" },
                new[] { "533005", "Layan", "Alqahtani",  "", "layan.alqahtani@stu.kfs.sch.sa", genderCode, "Grade 12", "1" },
                new[] { "533098", "Reem",  "Alotaibi",   "", "reem.alotaibi@stu.kfs.sch.sa", genderWord, "Grade 12", "2" }
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
    public Task<IReadOnlyList<StudentDto>> List([FromQuery] Guid eventId, [FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
        => _students.ListAsync(eventId, search, status, skip, take, ct);

    [HttpGet("{id:guid}")]
    public Task<StudentDto> Get(Guid id, [FromQuery] Guid? eventId, CancellationToken ct) => _students.GetAsync(id, eventId, ct);

    [HttpPatch("{id:guid}")]
    public Task<StudentDto> Update(Guid id, [FromQuery] Guid? eventId, [FromBody] UpdateStudentRequest request, CancellationToken ct)
        => _students.UpdateAsync(id, request, eventId ?? request.EventId, ct);

    [HttpPost("{id:guid}/reset-password")]
    public Task<ResetPasswordResponseDto> ResetPassword(Guid id, [FromQuery] Guid? eventId, CancellationToken ct)
        => _students.ResetPasswordAsync(id, eventId, ct);

    // Resets every active student to their initial password and emails each one a welcome message
    // with their sign-in credentials and the booking instructions. Scoped to one event.
    [HttpPost("send-welcome-emails")]
    public Task<SendWelcomeEmailsResponseDto> SendWelcomeEmails([FromQuery] Guid eventId, CancellationToken ct)
        => _students.SendWelcomeEmailsAsync(eventId, ct);

    // Destructive: removes every student + their bookings, items, guest passes, scan logs.
    [HttpDelete]
    public async Task<IActionResult> DeleteAll([FromQuery] Guid eventId, CancellationToken ct)
    {
        var deleted = await _students.DeleteAllAsync(eventId, ct);
        return Ok(new { deleted });
    }

    // Delete one student (FK-safe cascade — booking items + scans + their admin passes too).
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? eventId, CancellationToken ct)
    {
        await _students.DeleteAsync(id, eventId, ct);
        return Ok(new { deleted = 1 });
    }

    // Bulk delete N students by id. Body: { "ids": ["...", "..."] }.
    [HttpPost("delete-many")]
    public async Task<IActionResult> DeleteMany([FromQuery] Guid? eventId, [FromBody] DeleteManyRequest request, CancellationToken ct)
    {
        var deleted = await _students.DeleteManyAsync(request.Ids ?? Array.Empty<Guid>(), eventId ?? request.EventId, ct);
        return Ok(new { deleted });
    }

    public record DeleteManyRequest(Guid[]? Ids, Guid? EventId = null);
}
