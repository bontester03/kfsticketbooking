using KFS.Application.Common.Exceptions;
using KFS.Application.DTOs.Passes;
using KFS.Application.Interfaces;
using KFS.Domain.Entities;
using KFS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KFS.Application.Services;

public class GuestPassService : IGuestPassService
{
    private readonly IApplicationDbContext _db;
    private readonly IQrCodeService _qr;
    private readonly IBlobStorage _blobs;

    public GuestPassService(IApplicationDbContext db, IQrCodeService qr, IBlobStorage blobs)
    {
        _db = db; _qr = qr; _blobs = blobs;
    }

    public async Task<GuestPassDto> BookForStudentAsync(Guid studentId, Guid? issuedByAdminId, string? issuedToName, CancellationToken ct = default)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct)
            ?? throw new NotFoundException("Student", studentId);
        if (!student.IsActive) throw new AppException("inactive", "This student account is inactive.");

        // Scope every check to the student's event — boys event guest pool ≠ girls.
        var ev = await _db.Events.FindAsync(new object[] { student.EventId }, ct)
            ?? throw new AppException("no_event", "Student is not bound to an event.");

        // Each event has its own per-pass guest seat count (Boys=3, Girls=5).
        var guestSeats = ev.GuestSeatsPerPass;

        var existing = await _db.AdminPasses
            .FirstOrDefaultAsync(p => p.StudentId == studentId && p.Type == AdminPassType.Guest, ct);
        if (existing != null)
            throw new AppException("already_booked", "This child already has a guest ticket.");

        // Shared Guest pool — total guest seats issued for THIS event must fit in the zone capacity.
        var guestZone = await _db.Zones.FirstOrDefaultAsync(z => z.EventId == ev.Id && z.Code == ZoneCode.GUEST, ct);
        var capacity = guestZone?.Capacity ?? 0;
        var issued = await _db.AdminPasses
            .Where(p => p.EventId == ev.Id && p.Type == AdminPassType.Guest)
            .SumAsync(p => (int?)p.SeatsCount, ct) ?? 0;
        if (issued + guestSeats > capacity)
            throw new AppException("quota_exceeded",
                $"Guest tickets are sold out — {Math.Max(0, capacity - issued)} of {capacity} seats left.");

        var batchId = Guid.NewGuid();
        var ticket = $"KFS-GUEST-{batchId.ToString("N")[..6].ToUpperInvariant()}-001";
        var pass = new AdminPass
        {
            EventId = ev.Id,
            Type = AdminPassType.Guest,
            BatchId = batchId,
            SequenceNumber = 1,
            TicketNumber = ticket,
            SeatsCount = guestSeats,
            StudentId = studentId,
            IssuedByAdminId = issuedByAdminId,
            IssuedToName = string.IsNullOrWhiteSpace(issuedToName)
                ? $"{student.FirstName} {student.LastName}".Trim()
                : issuedToName.Trim(),
            IssuedAt = DateTime.UtcNow
        };
        pass.QrCodePayload = _qr.EncodePayload(new QrPayloadInput(
            pass.Id, ev.Id, ScannedItemType.AdminPass, ZoneCode.GUEST, null, guestSeats, ev.EventDate.AddHours(36)));
        var png = _qr.RenderPng(pass.QrCodePayload);
        pass.QrCodeImageUrl = await _blobs.SaveAsync($"qr-codes/{ev.Id}/{ticket}.png", png, "image/png", ct);

        _db.AdminPasses.Add(pass);
        await _db.SaveChangesAsync(ct);

        var gate = await GateForStudentAsync(studentId, ct);
        return Map(pass, student, 0, gate);
    }

    public async Task<GuestPassDto?> GetForStudentAsync(Guid studentId, CancellationToken ct = default)
    {
        var pass = await _db.AdminPasses
            .Include(p => p.Student)
            .FirstOrDefaultAsync(p => p.StudentId == studentId && p.Type == AdminPassType.Guest, ct);
        if (pass is null) return null;

        // Auto-heal the QR PNG file if the underlying blob/disk artifact went missing
        // (e.g. Railway container redeployed without a persistent volume mounted).
        // The QrCodePayload is durable in the DB, so we can always re-render the PNG
        // and re-upload it under the same blob path. Idempotent and cheap (~50ms).
        if (!string.IsNullOrEmpty(pass.QrCodePayload))
        {
            try
            {
                var png = _qr.RenderPng(pass.QrCodePayload);
                var refreshed = await _blobs.SaveAsync(
                    $"qr-codes/{pass.EventId}/{pass.TicketNumber}.png", png, "image/png", ct);
                if (!string.Equals(refreshed, pass.QrCodeImageUrl, StringComparison.Ordinal))
                {
                    pass.QrCodeImageUrl = refreshed;
                    await _db.SaveChangesAsync(ct);
                }
            }
            catch (Exception)
            {
                // Best-effort heal — don't block the page if blob storage is unreachable
                // momentarily. The Map() call below still returns whatever URL we have.
            }
        }

        var admitted = await AdmittedCountAsync(pass.Id, ct);
        var gate = await GateForStudentAsync(pass.StudentId, ct);
        return Map(pass, pass.Student, admitted, gate);
    }

    /// <summary>Student cancels their own guest ticket. Wipes the pass + its scan history.
    /// They can then re-book a fresh one via BookForStudentAsync. Blocks if the QR has
    /// already been scanned at the gate so attendance records stay clean.</summary>
    public async Task CancelForStudentAsync(Guid studentId, CancellationToken ct = default)
    {
        var pass = await _db.AdminPasses
            .FirstOrDefaultAsync(p => p.StudentId == studentId && p.Type == AdminPassType.Guest, ct)
            ?? throw new NotFoundException("Guest pass", studentId);

        // Once anyone has been admitted on this QR the student can't just void it —
        // they'd be discarding a successful gate entry. Admin override is the path for that.
        var admitted = await AdmittedCountAsync(pass.Id, ct);
        if (admitted > 0)
            throw new AppException("already_scanned",
                $"This guest ticket has already admitted {admitted} guest{(admitted == 1 ? "" : "s")} — ask the school office to cancel it for you.");

        // Drop any scan-log rows that reference this pass (defensive — there shouldn't
        // be any Valid scans, but Invalid / Expired entries can exist from QR misreads).
        var scans = await _db.ScanLogs
            .Where(s => s.ScannedItemType == ScannedItemType.AdminPass && s.ItemId == pass.Id)
            .ToListAsync(ct);
        if (scans.Count > 0) _db.ScanLogs.RemoveRange(scans);

        _db.AdminPasses.Remove(pass);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<GuestAnalyticsDto> GetAnalyticsAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await _db.Events.FindAsync(new object[] { eventId }, ct)
            ?? throw new NotFoundException("Event", eventId);

        var guestZone = await _db.Zones.FirstOrDefaultAsync(z => z.EventId == ev.Id && z.Code == ZoneCode.GUEST, ct);
        var limit = guestZone?.Capacity ?? 0;

        var passes = await _db.AdminPasses
            .Where(p => p.EventId == ev.Id && p.Type == AdminPassType.Guest)
            .Select(p => new { p.Id, p.SeatsCount, p.StudentId, p.IssuedByAdminId })
            .ToListAsync(ct);

        var issued = passes.Sum(p => p.SeatsCount);
        var bookedByStudents = passes.Count(p => p.StudentId != null && p.IssuedByAdminId == null);
        var issuedByAdminToChild = passes.Count(p => p.StudentId != null && p.IssuedByAdminId != null);
        var unassignedPool = passes.Count(p => p.StudentId == null);

        var passIds = passes.Select(p => p.Id).ToList();
        var admittedPeople = await _db.ScanLogs.CountAsync(s =>
            s.ScannedItemType == ScannedItemType.AdminPass && s.Result == ScanResult.Valid &&
            s.ItemId != null && passIds.Contains(s.ItemId.Value), ct);

        return new GuestAnalyticsDto(
            limit, issued, Math.Max(0, limit - issued),
            passes.Count, bookedByStudents, issuedByAdminToChild, unassignedPool, admittedPeople);
    }

    public async Task<IReadOnlyList<GuestEligibleStudentDto>> ListStudentsAsync(Guid eventId, string? search, CancellationToken ct = default)
    {
        // Only students assigned to THIS event — boys-event admins don't see girls students.
        var query = _db.Students.Where(s => s.IsActive && s.EventId == eventId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(x => x.Email.ToLower().Contains(s) || x.FirstName.ToLower().Contains(s) || x.LastName.ToLower().Contains(s));
        }
        var students = await query.OrderBy(x => x.LastName).Take(200).ToListAsync(ct);
        var ids = students.Select(s => s.Id).ToList();
        var withGuest = await _db.AdminPasses
            .Where(p => p.Type == AdminPassType.Guest && p.StudentId != null && ids.Contains(p.StudentId!.Value))
            .Select(p => p.StudentId!.Value).ToListAsync(ct);
        var withGuestSet = withGuest.ToHashSet();

        return students.Select(s => new GuestEligibleStudentDto(
            s.Id, $"{s.FirstName} {s.LastName}".Trim(), s.Email, withGuestSet.Contains(s.Id))).ToList();
    }

    private async Task<int> AdmittedCountAsync(Guid passId, CancellationToken ct) =>
        await _db.ScanLogs.CountAsync(s =>
            s.ItemId == passId && s.ScannedItemType == ScannedItemType.AdminPass && s.Result == ScanResult.Valid, ct);

    // The child's gate follows their VIP booking: VIP A booking → Gate A, VIP B → Gate B,
    // none → Gate A (default).
    private async Task<string> GateForStudentAsync(Guid? studentId, CancellationToken ct)
    {
        if (studentId is null) return "Gate A";
        var group = await _db.Bookings
            .Where(b => b.StudentId == studentId.Value && b.Status == BookingStatus.Confirmed)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => (ZoneGroup?)b.GroupChosen)
            .FirstOrDefaultAsync(ct);
        return group == ZoneGroup.B ? "Gate B" : "Gate A";
    }

    private GuestPassDto Map(AdminPass p, Student? student, int admitted, string gate) => new(
        p.Id, p.TicketNumber, p.SeatsCount, admitted, admitted >= p.SeatsCount,
        p.QrCodeImageUrl is null ? null : _blobs.RefreshReadUrl(p.QrCodeImageUrl),
        p.StudentId,
        student is null ? p.IssuedToName : $"{student.FirstName} {student.LastName}".Trim(),
        p.IssuedByAdminId != null, p.IssuedAt, gate);
}
