using KFS.Application.Common.Exceptions;
using KFS.Application.DTOs.Scan;
using KFS.Application.Interfaces;
using KFS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KFS.Application.Services;

public class ScanAuditService : IScanAuditService
{
    private readonly IApplicationDbContext _db;
    public ScanAuditService(IApplicationDbContext db) => _db = db;

    public async Task<ScanAuditDto> GetAuditAsync(string? search, string? status, string? kind, CancellationToken ct = default)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.IsActive, ct)
            ?? throw new AppException("no_active_event", "No active event.");

        // Valid-scan aggregates per item (count + first/last time), keyed by (itemId, type).
        var scanAgg = await _db.ScanLogs
            .Where(s => s.Result == ScanResult.Valid && s.ItemId != null)
            .GroupBy(s => new { s.ItemId, s.ScannedItemType })
            .Select(g => new { g.Key.ItemId, g.Key.ScannedItemType, Count = g.Count(), First = g.Min(x => x.ScannedAt), Last = g.Max(x => x.ScannedAt) })
            .ToListAsync(ct);
        var passScan = scanAgg.Where(x => x.ScannedItemType == ScannedItemType.AdminPass).ToDictionary(x => x.ItemId!.Value);
        var seatScan = scanAgg.Where(x => x.ScannedItemType == ScannedItemType.BookingItem).ToDictionary(x => x.ItemId!.Value);

        var rows = new List<ScanAuditRow>();

        // Admin passes (VVIP / Guest / Staff / Media, incl. student-booked guest tickets).
        var passes = await _db.AdminPasses.Where(p => p.EventId == ev.Id).Include(p => p.Student).ToListAsync(ct);
        foreach (var p in passes)
        {
            passScan.TryGetValue(p.Id, out var sc);
            var holder = p.Student != null ? $"{p.Student.FirstName} {p.Student.LastName}".Trim() : p.IssuedToName;
            rows.Add(new ScanAuditRow(p.Type.ToString(), p.TicketNumber, holder, null,
                p.SeatsCount, sc?.Count ?? 0, sc != null, sc?.First, sc?.Last));
        }

        // Student parent-seat tickets (confirmed bookings only).
        var items = await _db.BookingItems
            .Where(bi => _db.Bookings.Any(b => b.Id == bi.BookingId && b.EventId == ev.Id && b.Status == BookingStatus.Confirmed))
            .Include(bi => bi.Booking).ThenInclude(b => b!.Student)
            .Include(bi => bi.Zone).Include(bi => bi.Seat)
            .ToListAsync(ct);
        foreach (var i in items)
        {
            seatScan.TryGetValue(i.Id, out var sc);
            var student = i.Booking?.Student;
            var holder = student != null ? $"{i.ParentRole} of {student.FirstName} {student.LastName}".Trim() : i.ParentRole.ToString();
            var detail = $"{i.Zone?.DisplayName} · {i.Seat?.FullLabel}".Trim(' ', '·', ' ');
            rows.Add(new ScanAuditRow("Seat", i.TicketNumber, holder, detail,
                1, sc?.Count ?? 0, sc != null, sc?.First, sc?.Last));
        }

        IEnumerable<ScanAuditRow> q = rows;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            q = q.Where(r => r.TicketNumber.ToLowerInvariant().Contains(s)
                          || (r.Holder ?? "").ToLowerInvariant().Contains(s));
        }
        if (string.Equals(status, "scanned", StringComparison.OrdinalIgnoreCase)) q = q.Where(r => r.Scanned);
        if (string.Equals(status, "unscanned", StringComparison.OrdinalIgnoreCase)) q = q.Where(r => !r.Scanned);
        if (!string.IsNullOrWhiteSpace(kind)) q = q.Where(r => string.Equals(r.Kind, kind, StringComparison.OrdinalIgnoreCase));

        // Most recently scanned first; unscanned (null) sink to the bottom by ticket number.
        var ordered = q
            .OrderByDescending(r => r.LastScannedAt ?? DateTime.MinValue)
            .ThenBy(r => r.TicketNumber)
            .ToList();

        // Counters reflect the active filters so they match the rows shown.
        return new ScanAuditDto(
            ordered.Count,
            ordered.Count(r => r.Scanned),
            ordered.Sum(r => r.AdmittedCount),
            ordered);
    }
}
