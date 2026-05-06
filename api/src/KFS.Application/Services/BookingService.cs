using System.Data;
using KFS.Application.Common.Exceptions;
using KFS.Application.DTOs.Bookings;
using KFS.Application.Interfaces;
using KFS.Domain.Entities;
using KFS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace KFS.Application.Services;

public class BookingService : IBookingService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IQrCodeService _qr;
    private readonly IBlobStorage _blobs;
    private readonly IEmailService _email;
    private readonly ITicketEmailRenderer _emailRenderer;
    private readonly ISeatNotifier _notifier;
    private readonly ILogger<BookingService> _log;

    public BookingService(
        IApplicationDbContext db, ICurrentUser currentUser, IQrCodeService qr, IBlobStorage blobs,
        IEmailService email, ITicketEmailRenderer emailRenderer, ISeatNotifier notifier, ILogger<BookingService> log)
    {
        _db = db;
        _currentUser = currentUser;
        _qr = qr;
        _blobs = blobs;
        _email = email;
        _emailRenderer = emailRenderer;
        _notifier = notifier;
        _log = log;
    }

    public async Task<BookingDto?> GetCurrentCartAsync(CancellationToken ct = default)
    {
        var studentId = RequireStudent();
        var booking = await LoadActiveAsync(studentId, ct);
        if (booking is null || booking.Status != BookingStatus.Cart) return null;
        return Map(booking);
    }

    public async Task<BookingDto> SelectCartAsync(CartSelectRequest request, CancellationToken ct = default)
    {
        var studentId = RequireStudent();
        if (request.Group is not (ZoneGroup.A or ZoneGroup.B))
            throw new AppException("bad_input", "Group must be A or B.");
        if (request.Side is not (ZoneSide.Female or ZoneSide.Male))
            throw new AppException("bad_input", "Side must be Female or Male.");

        var ev = await _db.Events.FirstOrDefaultAsync(e => e.IsActive, ct)
            ?? throw new AppException("no_active_event", "No active event.", 409);

        var now = DateTime.UtcNow;
        if (now < ev.BookingOpensAt) throw new AppException("not_open", "Booking has not opened yet.");
        if (now > ev.BookingClosesAt) throw new AppException("closed", "Booking is closed.");

        // One active booking per student.
        var existing = await LoadActiveAsync(studentId, ct);
        if (existing != null && existing.Status is BookingStatus.Confirmed)
            throw new ConflictException("already_booked", "You already have a confirmed booking.");
        if (existing != null && existing.Status is BookingStatus.Cart)
            throw new ConflictException("cart_exists", "Release your current cart before picking a new seat.");

        var pickedZoneCode = ZoneCodeFor(request.Group, request.Side);
        var mirrorZoneCode = MirrorZoneCode(pickedZoneCode);

        // Resolve zones + seats up front (read).
        var zones = await _db.Zones
            .Where(z => z.EventId == ev.Id && (z.Code == pickedZoneCode || z.Code == mirrorZoneCode))
            .ToListAsync(ct);
        var pickedZone = zones.First(z => z.Code == pickedZoneCode);
        var mirrorZone = zones.First(z => z.Code == mirrorZoneCode);

        var seats = await _db.Seats
            .Where(s => (s.ZoneId == pickedZone.Id || s.ZoneId == mirrorZone.Id)
                        && s.RowLabel == request.RowLabel
                        && s.SeatNumber == request.SeatNumber)
            .ToListAsync(ct);
        if (seats.Count < 2)
            throw new NotFoundException("Seat", $"{request.RowLabel}-{request.SeatNumber}");

        var pickedSeat = seats.First(s => s.ZoneId == pickedZone.Id);
        var mirrorSeat = seats.First(s => s.ZoneId == mirrorZone.Id);

        var holdExpires = now.AddMinutes(ev.CartHoldMinutes);

        // Female-side seat → mother. Male-side seat → father. Decide once outside the loop.
        var (motherZone, motherSeat, fatherZone, fatherSeat) =
            pickedSeat.ZoneId == ZoneIdFor(zones, ZoneSide.Female)
                ? (pickedZone, pickedSeat, mirrorZone, mirrorSeat)
                : (mirrorZone, mirrorSeat, pickedZone, pickedSeat);

        // Reserve under Serializable isolation. Postgres can abort with SQLState 40001 if it
        // detects an inconsistency from a concurrent transaction; one retry is enough — the
        // caller's UI is also re-fetching the seat map via SignalR before they pick again.
        Booking booking = null!;
        const int maxAttempts = 2;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var tx = await _db.BeginSerializableTransactionAsync(ct);

                // Belt-and-suspenders: explicit row lock on both seats. Combined with serializable
                // isolation this makes the conflict check below race-free even if the DB engine
                // chooses to relax serializable to snapshot under load.
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM seats WHERE id = ANY({new[] { pickedSeat.Id, mirrorSeat.Id }}) FOR UPDATE", ct);

                var conflict = await _db.BookingItems
                    .Where(bi => (bi.SeatId == pickedSeat.Id || bi.SeatId == mirrorSeat.Id)
                                 && bi.Booking!.Status != BookingStatus.Cancelled
                                 && bi.Booking.Status != BookingStatus.Expired
                                 && (bi.Booking.Status == BookingStatus.Confirmed
                                     || bi.HoldExpiresAt > now))
                    .Select(bi => bi.SeatId)
                    .ToListAsync(ct);
                if (conflict.Count > 0)
                    throw new ConflictException("seat_taken", "That seat is no longer available.",
                        new { zone = pickedZoneCode.ToString(), row = request.RowLabel, seat = request.SeatNumber });

                booking = new Booking
                {
                    StudentId = studentId,
                    EventId = ev.Id,
                    Status = BookingStatus.Cart,
                    GroupChosen = request.Group
                };
                _db.Bookings.Add(booking);

                _db.BookingItems.Add(NewItem(booking.Id, motherZone, motherSeat, ParentRole.Mother, holdExpires));
                _db.BookingItems.Add(NewItem(booking.Id, fatherZone, fatherSeat, ParentRole.Father, holdExpires));

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                break;
            }
            catch (DbUpdateException ex) when (IsSerializationFailure(ex) && attempt < maxAttempts)
            {
                _log.LogWarning("Postgres 40001 serialization failure on cart-select attempt {Attempt}; retrying", attempt);
                _db.ChangeTracker.Clear();
            }
            catch (PostgresException pg) when (pg.SqlState == "40001" && attempt < maxAttempts)
            {
                _log.LogWarning("Postgres 40001 on cart-select attempt {Attempt}; retrying", attempt);
                _db.ChangeTracker.Clear();
            }
        }

        await BroadcastAsync(ev.Id, request.Group, ZoneSide.Female, motherSeatIdFor(pickedSeat, mirrorSeat, zones), "held", ct);
        await BroadcastAsync(ev.Id, request.Group, ZoneSide.Male, fatherSeatIdFor(pickedSeat, mirrorSeat, zones), "held", ct);

        return Map(await LoadAsync(booking.Id, ct));
    }

    public async Task ReleaseCartAsync(CancellationToken ct = default)
    {
        var studentId = RequireStudent();
        var booking = await LoadActiveAsync(studentId, ct);
        if (booking is null || booking.Status != BookingStatus.Cart) return;

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        foreach (var item in booking.Items)
            await BroadcastAsync(booking.EventId, booking.GroupChosen,
                item.Zone!.Side, item.SeatId, "released", ct);
    }

    public async Task<BookingDto> CheckoutAsync(CancellationToken ct = default)
    {
        var studentId = RequireStudent();
        var booking = await LoadActiveAsync(studentId, ct)
            ?? throw new NotFoundException("Cart", studentId);
        if (booking.Status != BookingStatus.Cart)
            throw new AppException("bad_state", "Cart is not in a checkable state.");

        if (booking.Items.Any(i => i.HoldExpiresAt < DateTime.UtcNow))
            throw new ConflictException("cart_expired", "Your cart hold expired — please pick again.");

        var student = await _db.Students.FindAsync(new object[] { studentId }, ct)
            ?? throw new NotFoundException("Student", studentId);
        var ev = await _db.Events.FindAsync(new object[] { booking.EventId }, ct)
            ?? throw new NotFoundException("Event", booking.EventId);

        booking.Status = BookingStatus.Confirmed;
        booking.ConfirmedAt = DateTime.UtcNow;
        var qrExpiry = ev.EventDate.AddHours(36);

        foreach (var item in booking.Items)
        {
            var ticketNumber = NewTicketNumber();
            item.TicketNumber = ticketNumber;
            item.QrCodePayload = _qr.EncodePayload(new QrPayloadInput(
                item.Id, ev.Id, ScannedItemType.BookingItem, item.Zone!.Code,
                item.Seat!.FullLabel, 1, qrExpiry));

            var png = _qr.RenderPng(item.QrCodePayload);
            item.QrCodeImageUrl = await _blobs.SaveAsync(
                $"qr-codes/{ev.Id}/{ticketNumber}.png", png, "image/png", ct);
        }

        await _db.SaveChangesAsync(ct);

        // Send 2 emails — one per ticket.
        foreach (var item in booking.Items)
        {
            var png = _qr.RenderPng(item.QrCodePayload!);
            var html = _emailRenderer.RenderTicket(new TicketEmailModel(
                StudentEmail: student.Email,
                ParentLabel: item.ParentRole == ParentRole.Mother ? "Mother" : "Father",
                StudentName: $"{student.FirstName} {student.LastName}",
                TicketLast6: item.TicketNumber[^6..],
                Group: booking.GroupChosen,
                Block: BlockLabel(item.Zone!.Code),
                Row: item.Seat!.RowLabel,
                SeatNumber: item.Seat.SeatNumber,
                EventName: ev.Name,
                EventDate: ev.EventDate,
                Venue: ev.Venue,
                MapLink: ev.MapLink), png);

            try
            {
                var msgId = await _email.SendAsync(new OutgoingEmail(
                    student.Email,
                    $"{ev.Name} — {item.ParentRole} ticket",
                    html,
                    new[] { new EmailAttachment($"{item.TicketNumber}.png", "image/png", png) }), ct);
                item.EmailSent = true;
                item.EmailSentAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send ticket email for booking item {ItemId}", item.Id);
            }
        }
        await _db.SaveChangesAsync(ct);

        foreach (var item in booking.Items)
            await BroadcastAsync(booking.EventId, booking.GroupChosen, item.Zone!.Side, item.SeatId, "booked", ct);

        return Map(booking);
    }

    public async Task<IReadOnlyList<BookingDto>> GetMyBookingsAsync(CancellationToken ct = default)
    {
        var studentId = RequireStudent();
        var bookings = await _db.Bookings
            .Where(b => b.StudentId == studentId)
            .Include(b => b.Items).ThenInclude(i => i.Zone)
            .Include(b => b.Items).ThenInclude(i => i.Seat)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);
        return bookings.Select(Map).ToList();
    }

    public async Task<BookingDto> CancelBookingAsync(Guid bookingId, CancellationToken ct = default)
    {
        var studentId = RequireStudent();
        var booking = await _db.Bookings
            .Include(b => b.Items).ThenInclude(i => i.Zone)
            .Include(b => b.Items).ThenInclude(i => i.Seat)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct)
            ?? throw new NotFoundException("Booking", bookingId);
        if (booking.StudentId != studentId)
            throw new ForbiddenException("Not your booking.");
        if (booking.Status != BookingStatus.Confirmed)
            throw new AppException("bad_state", "Only confirmed bookings can be cancelled.");

        var ev = await _db.Events.FindAsync(new object[] { booking.EventId }, ct)
            ?? throw new NotFoundException("Event", booking.EventId);

        booking.Status = BookingStatus.RebookWindow;
        booking.CancelledAt = DateTime.UtcNow;
        booking.RebookWindowExpiresAt = DateTime.UtcNow.AddMinutes(ev.CancellationWindowMinutes);
        await _db.SaveChangesAsync(ct);

        foreach (var item in booking.Items)
            await BroadcastAsync(booking.EventId, booking.GroupChosen, item.Zone!.Side, item.SeatId, "released", ct);

        return Map(booking);
    }

    public async Task ResendEmailsAsync(Guid bookingId, CancellationToken ct = default)
    {
        var studentId = RequireStudent();
        var booking = await _db.Bookings
            .Include(b => b.Items).ThenInclude(i => i.Zone)
            .Include(b => b.Items).ThenInclude(i => i.Seat)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.StudentId == studentId, ct)
            ?? throw new NotFoundException("Booking", bookingId);

        var student = await _db.Students.FindAsync(new object[] { studentId }, ct)
            ?? throw new NotFoundException("Student", studentId);
        var ev = await _db.Events.FindAsync(new object[] { booking.EventId }, ct)
            ?? throw new NotFoundException("Event", booking.EventId);

        foreach (var item in booking.Items)
        {
            var png = _qr.RenderPng(item.QrCodePayload!);
            var html = _emailRenderer.RenderTicket(new TicketEmailModel(
                student.Email, item.ParentRole == ParentRole.Mother ? "Mother" : "Father",
                $"{student.FirstName} {student.LastName}", item.TicketNumber[^6..],
                booking.GroupChosen, BlockLabel(item.Zone!.Code), item.Seat!.RowLabel,
                item.Seat.SeatNumber, ev.Name, ev.EventDate, ev.Venue, ev.MapLink), png);
            await _email.SendAsync(new OutgoingEmail(
                student.Email, $"{ev.Name} — {item.ParentRole} ticket (resend)", html,
                new[] { new EmailAttachment($"{item.TicketNumber}.png", "image/png", png) }), ct);
            item.EmailSent = true;
            item.EmailSentAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task ForceCancelAsync(Guid bookingId, CancellationToken ct = default)
    {
        var booking = await _db.Bookings
            .Include(b => b.Items).ThenInclude(i => i.Zone)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct)
            ?? throw new NotFoundException("Booking", bookingId);
        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        foreach (var item in booking.Items)
            await BroadcastAsync(booking.EventId, booking.GroupChosen, item.Zone!.Side, item.SeatId, "released", ct);
    }

    // ---- helpers ----

    private Guid RequireStudent()
    {
        if (_currentUser.UserType != UserType.Student) throw new ForbiddenException("Student-only action.");
        return _currentUser.UserId ?? throw new UnauthorizedException();
    }

    private async Task<Booking?> LoadActiveAsync(Guid studentId, CancellationToken ct) =>
        await _db.Bookings
            .Where(b => b.StudentId == studentId
                        && (b.Status == BookingStatus.Cart
                            || b.Status == BookingStatus.Confirmed
                            || b.Status == BookingStatus.RebookWindow))
            .Include(b => b.Items).ThenInclude(i => i.Zone)
            .Include(b => b.Items).ThenInclude(i => i.Seat)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(ct);

    private async Task<Booking> LoadAsync(Guid bookingId, CancellationToken ct) =>
        await _db.Bookings
            .Include(b => b.Items).ThenInclude(i => i.Zone)
            .Include(b => b.Items).ThenInclude(i => i.Seat)
            .FirstAsync(b => b.Id == bookingId, ct);

    private static BookingItem NewItem(Guid bookingId, Zone zone, Seat seat, ParentRole role, DateTime holdExpires) => new()
    {
        BookingId = bookingId,
        ZoneId = zone.Id,
        SeatId = seat.Id,
        ParentRole = role,
        TicketNumber = string.Empty,
        QrCodePayload = null,
        HoldExpiresAt = holdExpires
    };

    public static ZoneCode ZoneCodeFor(ZoneGroup group, ZoneSide side) => (group, side) switch
    {
        (ZoneGroup.A, ZoneSide.Female) => ZoneCode.VIPAF,
        (ZoneGroup.A, ZoneSide.Male)   => ZoneCode.VIPAM,
        (ZoneGroup.B, ZoneSide.Female) => ZoneCode.VIPBF,
        (ZoneGroup.B, ZoneSide.Male)   => ZoneCode.VIPBM,
        _ => throw new AppException("bad_input", "Unsupported group/side combination.")
    };

    public static ZoneCode MirrorZoneCode(ZoneCode code) => code switch
    {
        ZoneCode.VIPAF => ZoneCode.VIPAM,
        ZoneCode.VIPAM => ZoneCode.VIPAF,
        ZoneCode.VIPBF => ZoneCode.VIPBM,
        ZoneCode.VIPBM => ZoneCode.VIPBF,
        _ => throw new AppException("bad_input", "Zone has no mirror.")
    };

    public static string BlockLabel(ZoneCode code) => code switch
    {
        ZoneCode.VIPAF => "VIP AF",
        ZoneCode.VIPAM => "VIP AM",
        ZoneCode.VIPBF => "VIP BF",
        ZoneCode.VIPBM => "VIP BM",
        _ => code.ToString()
    };

    private static Guid ZoneIdFor(IEnumerable<Zone> zones, ZoneSide side)
        => zones.First(z => z.Side == side).Id;

    private static Guid motherSeatIdFor(Seat picked, Seat mirror, IEnumerable<Zone> zones)
    {
        var femaleZone = zones.First(z => z.Side == ZoneSide.Female);
        return picked.ZoneId == femaleZone.Id ? picked.Id : mirror.Id;
    }

    private static Guid fatherSeatIdFor(Seat picked, Seat mirror, IEnumerable<Zone> zones)
    {
        var maleZone = zones.First(z => z.Side == ZoneSide.Male);
        return picked.ZoneId == maleZone.Id ? picked.Id : mirror.Id;
    }

    private async Task BroadcastAsync(Guid eventId, ZoneGroup group, ZoneSide side, Guid seatId, string status, CancellationToken ct)
    {
        try { await _notifier.SeatChangedAsync(new SeatNotification(eventId, group, side, seatId, status), ct); }
        catch (Exception ex) { _log.LogWarning(ex, "Seat broadcast failed"); }
    }

    private static string NewTicketNumber() =>
        $"KFS{DateTime.UtcNow:yyMMdd}{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private static bool IsSerializationFailure(DbUpdateException ex)
    {
        for (var e = ex.InnerException; e != null; e = e.InnerException)
            if (e is PostgresException pg && pg.SqlState == "40001") return true;
        return false;
    }

    private static BookingDto Map(Booking b) => new(
        b.Id,
        b.StudentId,
        b.Status,
        b.GroupChosen,
        b.CreatedAt,
        b.ConfirmedAt,
        b.CancelledAt,
        b.RebookWindowExpiresAt,
        b.Items.OrderBy(i => i.ParentRole).Select(i => new BookingItemDto(
            i.Id, i.SeatId, BlockLabel(i.Zone?.Code ?? ZoneCode.VIPAF),
            i.Seat?.RowLabel ?? string.Empty, i.Seat?.SeatNumber ?? 0,
            i.Seat?.FullLabel ?? string.Empty,
            i.ParentRole, i.TicketNumber, i.QrCodeImageUrl, i.EmailSent, i.HoldExpiresAt)).ToList());
}
