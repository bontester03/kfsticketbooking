using KFS.Application.Common.Exceptions;
using KFS.Application.DTOs.SeatMap;
using KFS.Application.Interfaces;
using KFS.Domain.Entities;
using KFS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KFS.Application.Services;

public class SeatMapService : ISeatMapService
{
    private readonly IApplicationDbContext _db;
    public SeatMapService(IApplicationDbContext db) => _db = db;

    public async Task<SeatMapDto> GetAsync(Guid eventId, ZoneGroup group, bool includeOccupant, CancellationToken ct = default)
    {
        if (group is not (ZoneGroup.A or ZoneGroup.B))
            throw new AppException("bad_input", "Group must be A or B.");

        var femaleCode = BookingService.ZoneCodeFor(group, ZoneSide.Female);
        var maleCode = BookingService.ZoneCodeFor(group, ZoneSide.Male);

        var zones = await _db.Zones
            .Where(z => z.EventId == eventId && (z.Code == femaleCode || z.Code == maleCode))
            .Include(z => z.Seats)
            .ToListAsync(ct);
        if (zones.Count < 2) throw new NotFoundException("Zones", $"{group}/{femaleCode}/{maleCode}");

        var female = zones.First(z => z.Code == femaleCode);
        var male = zones.First(z => z.Code == maleCode);

        var seatIds = zones.SelectMany(z => z.Seats).Select(s => s.Id).ToHashSet();
        var now = DateTime.UtcNow;

        var occupants = await _db.BookingItems
            .Where(bi => seatIds.Contains(bi.SeatId)
                         && (bi.Booking!.Status == BookingStatus.Confirmed
                             || (bi.Booking.Status == BookingStatus.Cart && bi.HoldExpiresAt > now)))
            .Include(bi => bi.Booking)
                .ThenInclude(b => b!.Student)
            .ToListAsync(ct);

        var occupantBySeat = occupants.GroupBy(o => o.SeatId)
            .ToDictionary(g => g.Key, g => g.First());

        SeatMapZoneDto BuildZone(Zone z) => new(
            z.Id, z.Code, z.Side, z.DisplayName, z.Capacity,
            z.Seats.OrderBy(s => s.RowLabel).ThenBy(s => s.SeatNumber).Select(s =>
            {
                if (occupantBySeat.TryGetValue(s.Id, out var item))
                {
                    var status = item.Booking!.Status == BookingStatus.Confirmed ? SeatStatus.Booked : SeatStatus.Held;
                    var name = includeOccupant && item.Booking.Student != null
                        ? $"{item.Booking.Student.FirstName} {item.Booking.Student.LastName}"
                        : null;
                    return new SeatMapSeatDto(s.Id, s.RowLabel, s.SeatNumber, s.FullLabel, status, item.BookingId, name);
                }
                return new SeatMapSeatDto(s.Id, s.RowLabel, s.SeatNumber, s.FullLabel, SeatStatus.Available, null, null);
            }).ToList());

        return new SeatMapDto(group, BuildZone(female), BuildZone(male));
    }
}
