using ClosedXML.Excel;
using KFS.Application.DTOs.Reports;
using KFS.Application.Services;
using KFS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KFS.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/reports")]
public class AdminReportsController : ControllerBase
{
    private readonly IReportService _reports;
    public AdminReportsController(IReportService reports) => _reports = reports;

    [HttpGet("dashboard")]
    public Task<DashboardStatsDto> Dashboard([FromQuery] Guid eventId, CancellationToken ct)
        => _reports.GetDashboardAsync(eventId, ct);

    [HttpGet("group/{group}")]
    public async Task<IActionResult> GroupReport(string group, [FromQuery] Guid eventId,
        [FromQuery] string format, CancellationToken ct)
    {
        if (!Enum.TryParse<ZoneGroup>(group, true, out var g) || g is not (ZoneGroup.A or ZoneGroup.B))
            return BadRequest(new { code = "bad_input", message = "Group must be A or B." });

        var data = await _reports.GetGroupReportAsync(eventId, g, ct);

        return format.ToLowerInvariant() switch
        {
            "csv"  => File(await _reports.ExportCsvAsync(data, ct), "text/csv", $"group-{g}.csv"),
            "xlsx" => File(BuildXlsx(data), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"group-{g}.xlsx"),
            "pdf"  => File(BuildPdf(data), "application/pdf", $"group-{g}.pdf"),
            _ => BadRequest(new { code = "bad_input", message = "format must be csv, xlsx, or pdf" })
        };
    }

    private static byte[] BuildXlsx(GroupReportData data)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add($"Group {data.Group}");
        ws.Cell(1, 1).Value = $"{data.EventName} — Group {data.Group} ({data.EventDate:dd MMM yyyy})";
        ws.Range(1, 1, 1, 7).Merge().Style.Font.Bold = true;

        ws.Cell(2, 1).Value = "Row";
        ws.Cell(2, 2).Value = "Seat";
        ws.Cell(2, 3).Value = "Side";
        ws.Cell(2, 4).Value = "Parent Name";
        ws.Cell(2, 5).Value = "Linked Student";
        ws.Cell(2, 6).Value = "Email";
        ws.Cell(2, 7).Value = "Booked At";
        ws.Range(2, 1, 2, 7).Style.Font.Bold = true;

        for (var i = 0; i < data.Rows.Count; i++)
        {
            var r = data.Rows[i];
            ws.Cell(i + 3, 1).Value = r.RowLabel;
            ws.Cell(i + 3, 2).Value = r.SeatNumber;
            ws.Cell(i + 3, 3).Value = r.Side.ToString();
            ws.Cell(i + 3, 4).Value = r.ParentName;
            ws.Cell(i + 3, 5).Value = r.LinkedStudent;
            ws.Cell(i + 3, 6).Value = r.Email;
            ws.Cell(i + 3, 7).Value = r.BookedAt;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] BuildPdf(GroupReportData data) => Document.Create(c =>
    {
        c.Page(p =>
        {
            p.Size(PageSizes.A4);
            p.Margin(20);
            p.Header().Text($"{data.EventName} — Group {data.Group}").FontSize(14).Bold();
            p.Content().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(1); cd.RelativeColumn(1); cd.RelativeColumn(1.2f);
                    cd.RelativeColumn(3); cd.RelativeColumn(2.4f); cd.RelativeColumn(2.4f); cd.RelativeColumn(2);
                });
                t.Header(h =>
                {
                    foreach (var head in new[] { "Row", "Seat", "Side", "Parent", "Student", "Email", "Booked At" })
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(head).Bold();
                });
                foreach (var r in data.Rows)
                {
                    t.Cell().Padding(3).Text(r.RowLabel);
                    t.Cell().Padding(3).Text(r.SeatNumber.ToString());
                    t.Cell().Padding(3).Text(r.Side.ToString());
                    t.Cell().Padding(3).Text(r.ParentName);
                    t.Cell().Padding(3).Text(r.LinkedStudent);
                    t.Cell().Padding(3).Text(r.Email);
                    t.Cell().Padding(3).Text(r.BookedAt.ToString("dd MMM HH:mm"));
                }
            });
        });
    }).GeneratePdf();
}
