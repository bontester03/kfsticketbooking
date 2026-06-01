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
    private readonly IPassPdfRenderer _pdf;
    private readonly ISeatNotifier _notifier;
    private readonly ILogger<BookingService> _log;

    public BookingService(
        IApplicationDbContext db, ICurrentUser currentUser, IQrCodeService qr, IBlobStorage blobs,
        IEmailService email, ITicketEmailRenderer emailRenderer, IPassPdfRenderer pdf,
        ISeatNotifier notifier, ILogger<BookingService> log)
    {
        _db = db;
        _currentUser = currentUser;
        _qr = qr;
        _blobs = blobs;
        _email = email;
        _emailRenderer = emailRenderer;
        _pdf = pdf;
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

        // Enforce the school's pre-assigned VIP group. A student with AssignedGroup set may only
        // book seats in that group; nulls (legacy data) keep the old "pick any" behaviour.
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct)
            ?? throw new AppException("unauthorized", "Student not found.", 401);
        if (student.AssignedGroup.HasValue && student.AssignedGroup.Value != request.Group)
            throw new AppException("wrong_group",
                $"Your booking is restricted to VIP {(student.AssignedGroup.Value == ZoneGroup.A ? "A" : "B")}.");

        // Scope to THIS student's event — Boys student can never book a Girls-event seat.
        var ev = await _db.Events.FindAsync(new object[] { student.EventId }, ct)
            ?? throw new AppException("no_event", "Student is not bound to an event.", 409);

        var now = DateTime.UtcNow;
        if (now < ev.BookingOpensAt) throw new AppException("not_open", "Booking has not opened yet.");
        if (now > ev.BookingClosesAt) throw new AppException("closed", "Booking is closed.");

        // One active booking per student.
        var existing = await LoadActiveAsync(studentId, ct);
        if (existing != null && existing.Status is BookingStatus.Confirmed)
            throw new ConflictException("already_booked", "You already have a confirmed booking.");
        if (existing != null && existing.Status is BookingStatus.Cart)
            throw new ConflictException("cart_exists", "Release your current cart before picking a new seat.");

        // Girls event uses a single-block VIP A/B with pair-adjacency booking
        // (Mother + Grandmother on one QR). Boys keeps the F-side / M-side mirror flow below.
        if (ev.Gender == EventGender.Female)
            return await SelectCartGirlsAsync(studentId, ev, request, now, ct);

        if (request.Side is not (ZoneSide.Female or ZoneSide.Male))
            throw new AppException("bad_input", "Side must be Female or Male.");

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

    // ---------- Girls event: 1 QR per booking covering 2 ADJACENT seats ----------
    //
    // Pair rule: seats are paired (1,2), (3,4), (5,6), ... — picking an odd seat locks
    // odd+1, picking an even seat locks even−1. Same row, same zone (VIPA or VIPB; no
    // Female/Male side split for girls).
    //
    // The booking still owns TWO BookingItems (one per physical seat) so the seat map
    // shows both as taken. Only the Mother item carries a QrCodePayload — the
    // Grandmother item has NULL QR (Postgres unique indexes treat NULLs as distinct).
    // The scanner sees the Mother item's QR and admits up to 2 people per the event's
    // gender (handled in ScannerService).
    private async Task<BookingDto> SelectCartGirlsAsync(Guid studentId, Event ev,
        CartSelectRequest request, DateTime now, CancellationToken ct)
    {
        var zoneCode = request.Group == ZoneGroup.A ? ZoneCode.VIPA : ZoneCode.VIPB;
        var zone = await _db.Zones.FirstOrDefaultAsync(z => z.EventId == ev.Id && z.Code == zoneCode, ct)
            ?? throw new NotFoundException("Zone", zoneCode);

        // Pair the picked seat with its in-row neighbour.
        var picked = request.SeatNumber;
        var partner = picked % 2 == 1 ? picked + 1 : picked - 1;
        var seatNumbers = new[] { picked, partner };

        var seats = await _db.Seats
            .Where(s => s.ZoneId == zone.Id
                        && s.RowLabel == request.RowLabel
                        && seatNumbers.Contains(s.SeatNumber))
            .ToListAsync(ct);
        if (seats.Count != 2)
            throw new NotFoundException("Seat-pair",
                $"{request.RowLabel}-{picked} (partner {request.RowLabel}-{partner})");

        var motherSeat = seats.OrderBy(s => s.SeatNumber).First();
        var grandmotherSeat = seats.OrderBy(s => s.SeatNumber).Last();
        var holdExpires = now.AddMinutes(ev.CartHoldMinutes);

        Booking booking = null!;
        const int maxAttempts = 2;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var tx = await _db.BeginSerializableTransactionAsync(ct);

                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM seats WHERE id = ANY({new[] { motherSeat.Id, grandmotherSeat.Id }}) FOR UPDATE", ct);

                var conflict = await _db.BookingItems
                    .Where(bi => (bi.SeatId == motherSeat.Id || bi.SeatId == grandmotherSeat.Id)
                                 && bi.Booking!.Status != BookingStatus.Cancelled
                                 && bi.Booking.Status != BookingStatus.Expired
                                 && (bi.Booking.Status == BookingStatus.Confirmed
                                     || bi.HoldExpiresAt > now))
                    .Select(bi => bi.SeatId).ToListAsync(ct);
                if (conflict.Count > 0)
                    throw new ConflictException("seat_taken",
                        "That pair is no longer available — pick another seat.",
                        new { zone = zoneCode.ToString(), row = request.RowLabel, seat = picked });

                booking = new Booking
                {
                    StudentId = studentId,
                    EventId = ev.Id,
                    Status = BookingStatus.Cart,
                    GroupChosen = request.Group
                };
                _db.Bookings.Add(booking);

                _db.BookingItems.Add(NewItem(booking.Id, zone, motherSeat, ParentRole.Mother, holdExpires));
                _db.BookingItems.Add(NewItem(booking.Id, zone, grandmotherSeat, ParentRole.Grandmother, holdExpires));

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                break;
            }
            catch (DbUpdateException ex) when (IsSerializationFailure(ex) && attempt < maxAttempts)
            {
                _log.LogWarning("Postgres 40001 on girls cart-select attempt {Attempt}; retrying", attempt);
                _db.ChangeTracker.Clear();
            }
            catch (PostgresException pg) when (pg.SqlState == "40001" && attempt < maxAttempts)
            {
                _log.LogWarning("Postgres 40001 on girls cart-select attempt {Attempt}; retrying", attempt);
                _db.ChangeTracker.Clear();
            }
        }

        await BroadcastAsync(ev.Id, request.Group, ZoneSide.None, motherSeat.Id, "held", ct);
        await BroadcastAsync(ev.Id, request.Group, ZoneSide.None, grandmotherSeat.Id, "held", ct);

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

        // QR strategy:
        //   Boys event  → one QR per BookingItem (Mother + Father seat tickets are scanned separately).
        //   Girls event → ONE shared QR for the booking (Mother + Grandmother seats admit on the same
        //                 ticket). Only the Mother item carries QrCodePayload + QrCodeImageUrl + TicketNumber;
        //                 the Grandmother item leaves them NULL, which is legal because the unique
        //                 index on QrCodePayload treats NULLs as distinct.
        if (ev.Gender == EventGender.Female)
        {
            var motherItem = booking.Items.OrderBy(i => i.ParentRole).First();   // Mother (enum 0)
            var ticketNumber = NewTicketNumber();
            motherItem.TicketNumber = ticketNumber;
            motherItem.QrCodePayload = _qr.EncodePayload(new QrPayloadInput(
                motherItem.Id, ev.Id, ScannedItemType.BookingItem, motherItem.Zone!.Code,
                motherItem.Seat!.FullLabel, 2 /* admits 2 — handled by ScannerService */, qrExpiry));
            var png = _qr.RenderPng(motherItem.QrCodePayload);
            motherItem.QrCodeImageUrl = await _blobs.SaveAsync(
                $"qr-codes/{ev.Id}/{ticketNumber}.png", png, "image/png", ct);
            // Grandmother item: keep the same ticket number for human consistency,
            // but no QR — admin / scanner treat the Mother item as the canonical entry point.
            var grandmotherItem = booking.Items.OrderBy(i => i.ParentRole).Last();
            grandmotherItem.TicketNumber = ticketNumber;
        }
        else
        {
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
        }

        await _db.SaveChangesAsync(ct);

        // ONE email per booking with the styled multi-ticket PDF attached. The PDF
        // is the same layout the portal's "Download tickets PDF" button produces —
        // category badge / GATE / BLOCK / SEAT / ROW / Arabic pair / QR / receipt panel.
        await SendBookingEmailAsync(booking, student, ev, isResend: false, ct);

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

        // Annotate each ticket with its scan status so the child sees "scanned at the gate".
        var itemIds = bookings.SelectMany(b => b.Items.Select(i => i.Id)).ToList();
        var scans = await _db.ScanLogs
            .Where(s => s.ScannedItemType == ScannedItemType.BookingItem && s.Result == ScanResult.Valid
                        && s.ItemId != null && itemIds.Contains(s.ItemId.Value))
            .GroupBy(s => s.ItemId!.Value)
            .Select(g => new { ItemId = g.Key, ScannedAt = g.Min(x => x.ScannedAt) })
            .ToListAsync(ct);
        var scanByItem = scans.ToDictionary(x => x.ItemId, x => x.ScannedAt);

        return bookings.Select(b =>
        {
            var dto = Map(b);
            return dto with
            {
                Items = dto.Items.Select(it =>
                    scanByItem.TryGetValue(it.Id, out var at) ? it with { Scanned = true, ScannedAt = at } : it).ToList()
            };
        }).ToList();
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

        // Resend uses the same single-email + styled-PDF flow as the initial checkout.
        await SendBookingEmailAsync(booking, student, ev, isResend: true, ct);
        await _db.SaveChangesAsync(ct);
    }

    // ---------- Shared booking-email path ----------
    //
    // Renders the full booking PDF (one styled ticket card per parent — Mother+Father for
    // boys, Mother+Grandmother sharing one card for girls) and sends ONE email per booking
    // with the PDF attached. The HTML body keeps a short summary; the PDF is the artifact
    // the parents print or open on their phone.
    private async Task SendBookingEmailAsync(Booking booking, Student student, Event ev, bool isResend, CancellationToken ct)
    {
        var qrItems = booking.Items.Where(i => !string.IsNullOrEmpty(i.QrCodePayload))
                                    .OrderBy(i => i.ParentRole).ToList();
        if (qrItems.Count == 0) return;

        // Pair label is the same on every entry for one booking — both seats joined.
        var pairLabel = string.Join(" & ", booking.Items.OrderBy(i => i.ParentRole)
            .Select(i => i.Seat is null ? "" : $"{i.Seat.RowLabel}{i.Seat.SeatNumber}")
            .Where(s => s.Length > 0));
        var groupLetter = booking.GroupChosen == ZoneGroup.B ? "B" : "A";
        var studentName = $"{student.FirstName} {student.LastName}".Trim();

        // Build the StudentSeatTicketEntry list — boys: one entry per item; girls: one
        // entry (the Mother item carries the shared QR).
        var seats = qrItems.Select(item => new StudentSeatTicketEntry(
            Group:        groupLetter,
            Row:          item.Seat?.RowLabel ?? "",
            Seat:         item.Seat?.SeatNumber ?? 0,
            ParentRole:   ev.Gender == EventGender.Female
                            ? ev.PairLabel
                            : ParentRoleLabels.Label(item.ParentRole, ev.Gender),
            StudentName:  studentName,
            StudentEmail: student.Email,
            TicketNumber: item.TicketNumber,
            PairLabel:    pairLabel,
            QrPng:        _qr.RenderPng(item.QrCodePayload!))).ToList();

        var pdfBytes = _pdf.RenderStudentTickets(ev.Name, ev.EventDate, studentName, seats, guest: null);

        var roleSummary = ev.Gender == EventGender.Female
            ? "Mother & Grandmother"
            : string.Join(" & ", qrItems.Select(i => ParentRoleLabels.Label(i.ParentRole, ev.Gender)));
        var subjectSuffix = isResend ? " (resend)" : "";
        var subject = $"{ev.Name} — {roleSummary} ticket{(qrItems.Count == 1 ? "" : "s")}{subjectSuffix}";

        // Plain-but-on-brand HTML body — the PDF carries the styled visuals.
        var html = $@"<div style=""font-family:Arial,sans-serif;max-width:560px;margin:auto;color:#14241f"">
  <h2 style=""color:#0d3128;margin:0 0 12px"">{System.Net.WebUtility.HtmlEncode(ev.Name)}</h2>
  <p style=""font-size:14px"">Hello {System.Net.WebUtility.HtmlEncode(student.FirstName)},</p>
  <p>Your tickets for <strong>{System.Net.WebUtility.HtmlEncode(roleSummary)}</strong> are attached as a PDF.</p>
  <ul style=""font-size:14px;line-height:1.7"">
    <li><strong>Block / Seats:</strong> VIP {System.Net.WebUtility.HtmlEncode(groupLetter)} · {System.Net.WebUtility.HtmlEncode(pairLabel)}</li>
    <li><strong>Date:</strong> {ev.EventDate:dd MMM yyyy}</li>
    <li><strong>Venue:</strong> {System.Net.WebUtility.HtmlEncode(ev.Venue)}</li>
  </ul>
  <p style=""font-size:13px;color:#475569"">Open the PDF on your phone (or print it) and present each QR code at the gate.</p>
  <p style=""font-size:12px;color:#94a3b8;margin-top:18px"">King Faisal School — Event Management</p>
</div>";

        // Filename: first ticket's number is good enough for grouping in the inbox.
        var fileName = $"kfs-tickets-{qrItems.First().TicketNumber}.pdf";

        try
        {
            await _email.SendAsync(new OutgoingEmail(
                student.Email,
                subject,
                html,
                new[] { new EmailAttachment(fileName, "application/pdf", pdfBytes) }), ct);

            foreach (var item in booking.Items)
            {
                item.EmailSent = true;
                item.EmailSentAt = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send booking-email for booking {BookingId}", booking.Id);
        }
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

    /// Static for cases where a fresh URL isn't necessary (concurrency tests, e.g.).
    private static BookingDto MapStatic(Booking b) => new(
        b.Id, b.StudentId, b.Status, b.GroupChosen, b.CreatedAt,
        b.ConfirmedAt, b.CancelledAt, b.RebookWindowExpiresAt,
        b.Items.OrderBy(i => i.ParentRole).Select(i => new BookingItemDto(
            i.Id, i.SeatId, BlockLabel(i.Zone?.Code ?? ZoneCode.VIPAF),
            i.Seat?.RowLabel ?? string.Empty, i.Seat?.SeatNumber ?? 0,
            i.Seat?.FullLabel ?? string.Empty,
            i.ParentRole, i.TicketNumber, i.QrCodeImageUrl,
            i.EmailSent, i.HoldExpiresAt)).ToList());

    /// Instance Map — refreshes the SAS on each QR URL so a client opening the page hours
    /// after checkout still gets a currently-valid signed URL.
    private BookingDto Map(Booking b) => new(
        b.Id, b.StudentId, b.Status, b.GroupChosen, b.CreatedAt,
        b.ConfirmedAt, b.CancelledAt, b.RebookWindowExpiresAt,
        b.Items.OrderBy(i => i.ParentRole).Select(i => new BookingItemDto(
            i.Id, i.SeatId, BlockLabel(i.Zone?.Code ?? ZoneCode.VIPAF),
            i.Seat?.RowLabel ?? string.Empty, i.Seat?.SeatNumber ?? 0,
            i.Seat?.FullLabel ?? string.Empty,
            i.ParentRole, i.TicketNumber,
            i.QrCodeImageUrl is null ? null : _blobs.RefreshReadUrl(i.QrCodeImageUrl),
            i.EmailSent, i.HoldExpiresAt)).ToList());
}
