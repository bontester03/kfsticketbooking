namespace KFS.Application.DTOs.Events;

public record EventDto(
    Guid Id,
    string Name,
    DateTime EventDate,
    string Venue,
    string VenueAddress,
    string? MapLink,
    bool IsActive,
    DateTime BookingOpensAt,
    DateTime BookingClosesAt,
    int CartHoldMinutes,
    int CancellationWindowMinutes,
    string? ReminderNoteFromAdmin,
    string ScannerToken);

public record UpdateEventRequest(
    string Name,
    DateTime EventDate,
    string Venue,
    string VenueAddress,
    string? MapLink,
    bool IsActive,
    DateTime BookingOpensAt,
    DateTime BookingClosesAt,
    int CartHoldMinutes,
    int CancellationWindowMinutes,
    string? ReminderNoteFromAdmin);
