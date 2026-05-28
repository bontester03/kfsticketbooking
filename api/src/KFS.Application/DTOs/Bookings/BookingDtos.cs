using KFS.Domain.Enums;

namespace KFS.Application.DTOs.Bookings;

public record CartSelectRequest(ZoneGroup Group, ZoneSide Side, string RowLabel, int SeatNumber);

public record BookingItemDto(
    Guid Id,
    Guid SeatId,
    string Block,
    string RowLabel,
    int SeatNumber,
    string FullLabel,
    ParentRole ParentRole,
    string TicketNumber,
    string? QrCodeImageUrl,
    bool EmailSent,
    DateTime HoldExpiresAt,
    bool Scanned = false,
    DateTime? ScannedAt = null);

public record BookingDto(
    Guid Id,
    Guid StudentId,
    BookingStatus Status,
    ZoneGroup GroupChosen,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? CancelledAt,
    DateTime? RebookWindowExpiresAt,
    IReadOnlyList<BookingItemDto> Items);
