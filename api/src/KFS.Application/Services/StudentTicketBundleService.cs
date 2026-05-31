using KFS.Application.Common.Exceptions;
using KFS.Application.Interfaces;
using KFS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KFS.Application.Services;

public class StudentTicketBundleService : IStudentTicketBundleService
{
    private readonly IApplicationDbContext _db;
    private readonly IQrCodeService _qr;
    private readonly IPassPdfRenderer _pdf;

    public StudentTicketBundleService(IApplicationDbContext db, IQrCodeService qr, IPassPdfRenderer pdf)
    {
        _db = db; _qr = qr; _pdf = pdf;
    }

    public async Task<(byte[] Bytes, string FileName)> BuildAsync(Guid studentId, CancellationToken ct = default)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct)
            ?? throw new NotFoundException("Student", studentId);
        var ev = await _db.Events.FindAsync(new object[] { student.EventId }, ct)
            ?? throw new AppException("no_event", "Student is not bound to an event.");

        // Confirmed parent passes for this child (most recent first).
        var bookings = await _db.Bookings
            .Where(b => b.StudentId == studentId && b.EventId == ev.Id && b.Status == BookingStatus.Confirmed)
            .Include(b => b.Items).ThenInclude(i => i.Zone)
            .Include(b => b.Items).ThenInclude(i => i.Seat)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

        var seats = new List<StudentSeatTicketEntry>();
        var studentName = $"{student.FirstName} {student.LastName}".Trim();
        foreach (var b in bookings)
        {
            var group = b.GroupChosen == ZoneGroup.B ? "B" : "A";
            // Both seats of the pair on the Arabic line — "A12 & A11".
            var pairLabel = string.Join(" & ",
                b.Items.OrderBy(i => i.ParentRole)
                    .Select(i => i.Seat is null ? "" : $"{i.Seat.RowLabel}{i.Seat.SeatNumber}")
                    .Where(s => !string.IsNullOrEmpty(s)));
            // Girls bookings carry the QR on the Mother item only — render ONE PDF entry
            // for the pair so the student doesn't see two tickets for a single QR.
            // Boys bookings get one entry per item as before.
            var itemsToRender = ev.Gender == EventGender.Female
                ? b.Items.Where(i => !string.IsNullOrEmpty(i.QrCodePayload)).OrderBy(i => i.ParentRole)
                : b.Items.OrderBy(i => i.ParentRole);

            foreach (var item in itemsToRender)
            {
                var roleLabel = ev.Gender == EventGender.Female
                    ? ev.PairLabel
                    : KFS.Domain.Enums.ParentRoleLabels.Label(item.ParentRole, ev.Gender);
                seats.Add(new StudentSeatTicketEntry(
                    Group: group,
                    Row: item.Seat?.RowLabel ?? "",
                    Seat: item.Seat?.SeatNumber ?? 0,
                    ParentRole: roleLabel,
                    StudentName: studentName,
                    StudentEmail: student.Email,
                    TicketNumber: item.TicketNumber,
                    PairLabel: pairLabel,
                    QrPng: _qr.RenderPng(item.QrCodePayload ?? "")));
            }
        }

        // Optional guest ticket.
        var guestPass = await _db.AdminPasses
            .FirstOrDefaultAsync(p => p.StudentId == studentId && p.Type == AdminPassType.Guest, ct);
        PassPdfEntry? guestEntry = null;
        if (guestPass is not null)
        {
            // Gate follows the child's VIP booking (A → Gate A, B → Gate B), default Gate A.
            var gate = bookings.FirstOrDefault()?.GroupChosen == ZoneGroup.B ? "Gate B" : "Gate A";
            guestEntry = new PassPdfEntry(
                guestPass.TicketNumber, guestPass.SequenceNumber,
                _qr.RenderPng(guestPass.QrCodePayload), guestPass.SeatsCount,
                $"{student.FirstName} {student.LastName}".Trim(), gate);
        }

        if (seats.Count == 0 && guestEntry is null)
            throw new AppException("nothing_to_print", "No confirmed tickets yet.");

        var bytes = _pdf.RenderStudentTickets(ev.Name, ev.EventDate,
            $"{student.FirstName} {student.LastName}".Trim(), seats, guestEntry);

        var safeName = $"{student.FirstName}-{student.LastName}".ToLowerInvariant().Replace(' ', '-');
        return (bytes, $"{safeName}-tickets.pdf");
    }
}
