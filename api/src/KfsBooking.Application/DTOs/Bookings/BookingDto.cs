using KfsBooking.Domain.Enums;

namespace KfsBooking.Application.DTOs.Bookings;

public record BookingDto(
    Guid Id,
    Guid UserId,
    string UserName,
    Guid AuditoriumId,
    string AuditoriumName,
    string Purpose,
    DateTime StartTime,
    DateTime EndTime,
    BookingStatus Status,
    string? RejectionReason,
    DateTime CreatedAt);

public record CreateBookingRequest(
    Guid AuditoriumId,
    string Purpose,
    DateTime StartTime,
    DateTime EndTime);

public record UpdateBookingStatusRequest(
    BookingStatus Status,
    string? RejectionReason);
