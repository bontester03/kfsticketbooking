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
