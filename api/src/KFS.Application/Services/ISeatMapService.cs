using KFS.Application.DTOs.SeatMap;
using KFS.Domain.Enums;

namespace KFS.Application.Services;

public interface ISeatMapService
{
    Task<SeatMapDto> GetAsync(Guid eventId, ZoneGroup group, bool includeOccupant, CancellationToken ct = default);
}
