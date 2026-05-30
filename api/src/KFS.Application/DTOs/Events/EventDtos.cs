using KFS.Domain.Enums;

namespace KFS.Application.DTOs.Events;

public record EventDto(
    Guid Id,
    string Name,
    string Slug,
    EventGender Gender,
    string PairLabel,
    int GuestSeatsPerPass,
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

// Safe, aggregate event info for the pre-auth landing/sign-in banner. No PII.
public record PublicEventDto(
    string Name,
    string Slug,
    EventGender Gender,
    DateTime EventDate,
    string Venue,
    string VenueAddress,
    DateTime BookingOpensAt,
    DateTime BookingClosesAt,
    int SeatsTotal,
    int SeatsRemaining);

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
