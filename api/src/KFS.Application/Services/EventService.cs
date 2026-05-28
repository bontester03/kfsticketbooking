using KFS.Application.Common.Exceptions;
using KFS.Application.DTOs.Events;
using KFS.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KFS.Application.Services;

public class EventService : IEventService
{
    private readonly IApplicationDbContext _db;
    public EventService(IApplicationDbContext db) => _db = db;

    public async Task<EventDto> GetActiveAsync(CancellationToken ct = default)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.IsActive, ct)
            ?? throw new NotFoundException("Event", "active");
        return Map(ev);
    }

    public async Task<PublicEventDto?> GetPublicSummaryAsync(CancellationToken ct = default)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.IsActive, ct);
        if (ev is null) return null;

        // Bookable student seats live in the reserved-seating (VIP) zones.
        var reservedZoneIds = await _db.Zones
            .Where(z => z.EventId == ev.Id && z.IsReservedSeating)
            .Select(z => z.Id).ToListAsync(ct);

        var seatsTotal = await _db.Seats.CountAsync(s => reservedZoneIds.Contains(s.ZoneId), ct);

        var activeStatuses = new[]
        {
            Domain.Enums.BookingStatus.Confirmed,
            Domain.Enums.BookingStatus.Cart,
            Domain.Enums.BookingStatus.RebookWindow
        };
        var taken = await _db.BookingItems.CountAsync(bi =>
            _db.Bookings.Any(b => b.Id == bi.BookingId && b.EventId == ev.Id && activeStatuses.Contains(b.Status)), ct);

        return new PublicEventDto(
            ev.Name, ev.EventDate, ev.Venue, ev.VenueAddress,
            ev.BookingOpensAt, ev.BookingClosesAt,
            seatsTotal, Math.Max(0, seatsTotal - taken));
    }

    public async Task<EventDto> UpdateAsync(Guid eventId, UpdateEventRequest r, CancellationToken ct = default)
    {
        var ev = await _db.Events.FindAsync(new object[] { eventId }, ct)
            ?? throw new NotFoundException("Event", eventId);
        ev.Name = r.Name.Trim();
        ev.EventDate = r.EventDate;
        ev.Venue = r.Venue.Trim();
        ev.VenueAddress = r.VenueAddress.Trim();
        ev.MapLink = r.MapLink;
        ev.IsActive = r.IsActive;
        ev.BookingOpensAt = r.BookingOpensAt;
        ev.BookingClosesAt = r.BookingClosesAt;
        ev.CartHoldMinutes = r.CartHoldMinutes;
        ev.CancellationWindowMinutes = r.CancellationWindowMinutes;
        ev.ReminderNoteFromAdmin = r.ReminderNoteFromAdmin;
        await _db.SaveChangesAsync(ct);
        return Map(ev);
    }

    private static EventDto Map(Domain.Entities.Event ev) => new(
        ev.Id, ev.Name, ev.EventDate, ev.Venue, ev.VenueAddress, ev.MapLink, ev.IsActive,
        ev.BookingOpensAt, ev.BookingClosesAt, ev.CartHoldMinutes, ev.CancellationWindowMinutes,
        ev.ReminderNoteFromAdmin, ev.ScannerToken);
}
