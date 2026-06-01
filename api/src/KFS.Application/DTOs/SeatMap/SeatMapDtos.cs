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

// Boys event returns TWO zones per group (Female-side + Male-side); girls returns
// ONE single-block zone per group. The shape is now a list so the UI can render
// either flavour without knowing the event gender up front.
public record SeatMapDto(
    ZoneGroup Group,
    IReadOnlyList<SeatMapZoneDto> Zones);
