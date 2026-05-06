using KFS.Domain.Enums;

namespace KFS.Application.DTOs.SeatMap;

public enum SeatStatus
{
    Available = 0,
    Held = 1,
    Booked = 2
}

public record SeatMapSeatDto(
    Guid Id,
    string RowLabel,
    int SeatNumber,
    string FullLabel,
    SeatStatus Status,
    Guid? OccupantBookingId,
    string? OccupantName);

public record SeatMapZoneDto(
    Guid ZoneId,
    ZoneCode Code,
    ZoneSide Side,
    string DisplayName,
    int Capacity,
    IReadOnlyList<SeatMapSeatDto> Seats);

public record SeatMapDto(
    ZoneGroup Group,
    SeatMapZoneDto FemaleZone,
    SeatMapZoneDto MaleZone);
