using KFS.Application.Common.Exceptions;
using KFS.Application.DTOs.Events;
using KFS.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KFS.Application.Services;

public class EventService : IEventService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public EventService(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<EventDto>> ListAsync(CancellationToken ct = default)
    {
        var list = await _db.Events.OrderBy(e => e.Gender).ToListAsync(ct);
        return list.Select(Map).ToList();
    }

    public async Task<EventDto> GetByIdAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await _db.Events.FindAsync(new object[] { eventId }, ct)
            ?? throw new NotFoundException("Event", eventId);
        return Map(ev);
    }

    public async Task<EventDto> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Slug == slug, ct)
            ?? throw new NotFoundException("Event", slug);
        return Map(ev);
    }

    public async Task<EventDto> GetForCurrentStudentAsync(CancellationToken ct = default)
    {
        var eventId = _currentUser.EventId
            ?? throw new UnauthorizedException("Student is not bound to an event.");
        return await GetByIdAsync(eventId, ct);
    }

    public async Task<IReadOnlyList<PublicEventDto>> ListPublicSummariesAsync(CancellationToken ct = default)
    {
        var events = await _db.Events.OrderBy(e => e.Gender).ToListAsync(ct);
        var summaries = new List<PublicEventDto>(events.Count);

        var activeStatuses = new[]
        {
            Domain.Enums.BookingStatus.Confirmed,
            Domain.Enums.BookingStatus.Cart,
            Domain.Enums.BookingStatus.RebookWindow
        };

        foreach (var ev in events)
        {
            var reservedZoneIds = await _db.Zones
                .Where(z => z.EventId == ev.Id && z.IsReservedSeating
                            && z.Visibility == Domain.Enums.ZoneVisibility.PublicBookable)
                .Select(z => z.Id).ToListAsync(ct);

            var seatsTotal = await _db.Seats.CountAsync(s => reservedZoneIds.Contains(s.ZoneId), ct);
            var taken = await _db.BookingItems.CountAsync(bi =>
                _db.Bookings.Any(b => b.Id == bi.BookingId && b.EventId == ev.Id && activeStatuses.Contains(b.Status)), ct);

            summaries.Add(new PublicEventDto(
                ev.Name, ev.Slug, ev.Gender, ev.EventDate, ev.Venue, ev.VenueAddress,
                ev.BookingOpensAt, ev.BookingClosesAt,
                seatsTotal, Math.Max(0, seatsTotal - taken)));
        }

        return summaries;
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
        ev.Id, ev.Name, ev.Slug, ev.Gender, ev.PairLabel, ev.GuestSeatsPerPass,
        ev.EventDate, ev.Venue, ev.VenueAddress, ev.MapLink, ev.IsActive,
        ev.BookingOpensAt, ev.BookingClosesAt, ev.CartHoldMinutes, ev.CancellationWindowMinutes,
        ev.ReminderNoteFromAdmin, ev.ScannerToken);
}
