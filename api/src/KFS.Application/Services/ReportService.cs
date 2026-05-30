using System.Globalization;
using System.Text;
using KFS.Application.Common.Exceptions;
using KFS.Application.DTOs.Reports;
using KFS.Application.Interfaces;
using KFS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KFS.Application.Services;

public class ReportService : IReportService
{
    private readonly IApplicationDbContext _db;
    private readonly IPassPdfRenderer _pdf;

    public ReportService(IApplicationDbContext db, IPassPdfRenderer pdf)
    {
        _db = db; _pdf = pdf;
    }

    public async Task<GroupReportData> GetGroupReportAsync(Guid eventId, ZoneGroup group, CancellationToken ct = default)
    {
        if (group is not (ZoneGroup.A or ZoneGroup.B))
            throw new AppException("bad_input", "Group must be A or B.");

        var ev = await _db.Events.FindAsync(new object[] { eventId }, ct)
            ?? throw new NotFoundException("Event", eventId);

        // Codes for the requested group, covering both event styles:
        //  - Boys event splits the VIP block into Female/Male zones (VIPAF / VIPAM)
        //  - Girls event uses one block per group (VIPA / VIPB)
        var codes = group == ZoneGroup.A
            ? new[] { ZoneCode.VIPAF, ZoneCode.VIPAM, ZoneCode.VIPA }
            : new[] { ZoneCode.VIPBF, ZoneCode.VIPBM, ZoneCode.VIPB };

        var rows = await _db.BookingItems
            .Where(bi => bi.Booking!.Status == BookingStatus.Confirmed
                         && bi.Booking.EventId == ev.Id
                         && codes.Contains(bi.Zone!.Code))
            .Include(bi => bi.Booking).ThenInclude(b => b!.Student)
            .Include(bi => bi.Zone)
            .Include(bi => bi.Seat)
            .Select(bi => new
            {
                bi.Seat!.RowLabel,
                bi.Seat.SeatNumber,
                bi.Zone!.Side,
                bi.ParentRole,
                Student = bi.Booking!.Student!,
                BookedAt = bi.Booking.ConfirmedAt ?? bi.Booking.CreatedAt
            })
            .ToListAsync(ct);

        var reportRows = rows.Select(r => new GroupReportRow(
            r.RowLabel, r.SeatNumber, r.Side,
            $"{r.ParentRole} of {r.Student.FirstName} {r.Student.LastName}",
            $"{r.Student.FirstName} {r.Student.LastName}",
            r.Student.Email, r.BookedAt))
            .OrderBy(r => r.RowLabel).ThenBy(r => r.SeatNumber).ThenBy(r => r.Side).ToList();

        return new GroupReportData(group, ev.Name, ev.EventDate, reportRows);
    }

    public Task<byte[]> ExportXlsxAsync(GroupReportData data, CancellationToken ct = default)
    {
        // Implemented in Infrastructure (ClosedXMLReportRenderer), but we can do a CSV fallback
        // here using ClosedXML directly is heavy; defer to PDF/CSV by default.
        throw new NotImplementedException("Excel export is implemented via ClosedXMLReportRenderer in Infrastructure.");
    }

    public Task<byte[]> ExportPdfAsync(GroupReportData data, CancellationToken ct = default)
    {
        // PDF report rendering deferred to Infrastructure (uses QuestPDF).
        throw new NotImplementedException("PDF export is implemented via Infrastructure ReportPdfRenderer.");
    }

    public Task<byte[]> ExportCsvAsync(GroupReportData data, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Row,Seat,Side,Parent Name,Linked Student,Email,Booked At");
        foreach (var r in data.Rows)
            sb.AppendLine(string.Join(",", new[]
            {
                Csv(r.RowLabel), r.SeatNumber.ToString(CultureInfo.InvariantCulture), r.Side.ToString(),
                Csv(r.ParentName), Csv(r.LinkedStudent), Csv(r.Email),
                r.BookedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            }));
        return Task.FromResult(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public async Task<DashboardStatsDto> GetDashboardAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await _db.Events.FindAsync(new object[] { eventId }, ct)
            ?? throw new NotFoundException("Event", eventId);

        // Per-event student count.
        var totalStudents = await _db.Students.CountAsync(s => s.IsActive && s.EventId == ev.Id, ct);
        var bookings = await _db.Bookings.Where(b => b.EventId == ev.Id)
            .GroupBy(b => b.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);

        int Get(BookingStatus s) => bookings.FirstOrDefault(b => b.Key == s)?.Count ?? 0;

        var loggedInProxy = await _db.RefreshTokens.Select(t => t.UserId).Distinct().CountAsync(ct);

        var today = DateTime.UtcNow.Date;
        var scansToday = await _db.ScanLogs.CountAsync(s => s.EventId == ev.Id && s.ScannedAt >= today, ct);

        var zones = await _db.Zones.Where(z => z.EventId == ev.Id).ToListAsync(ct);
        var bookingZoneCounts = await _db.BookingItems
            .Where(bi => bi.Booking!.Status == BookingStatus.Confirmed && bi.Booking.EventId == ev.Id)
            .GroupBy(bi => bi.ZoneId).Select(g => new { ZoneId = g.Key, Count = g.Count() }).ToListAsync(ct);
        var passZoneCounts = await _db.AdminPasses.Where(p => p.EventId == ev.Id)
            .GroupBy(p => p.Type).Select(g => new { Type = g.Key, SeatsTotal = g.Sum(x => x.SeatsCount) }).ToListAsync(ct);

        var zoneStats = zones.Select(z =>
        {
            var issued = z.IsReservedSeating
                ? bookingZoneCounts.FirstOrDefault(x => x.ZoneId == z.Id)?.Count ?? 0
                : passZoneCounts.FirstOrDefault(p => MapToPassType(z.Code) == p.Type)?.SeatsTotal ?? 0;
            var pct = z.Capacity == 0 ? 0 : (double)issued / z.Capacity;
            return new ZoneCapacityDto(z.DisplayName, z.Capacity, issued, pct);
        }).ToList();

        return new DashboardStatsDto(totalStudents, loggedInProxy,
            Get(BookingStatus.Cart), Get(BookingStatus.Confirmed), Get(BookingStatus.Cancelled), scansToday, zoneStats);
    }

    private static AdminPassType? MapToPassType(ZoneCode code) => code switch
    {
        ZoneCode.GUEST => AdminPassType.Guest,
        ZoneCode.STAFF => AdminPassType.Staff,
        ZoneCode.MEDIA => AdminPassType.Media,
        ZoneCode.VVIP  => AdminPassType.VVIP,
        _ => null
    };

    private static string Csv(string? s)
    {
        if (s is null) return "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
