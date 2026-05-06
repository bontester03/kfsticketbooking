using KFS.Application.DTOs.Bookings;

namespace KFS.Application.Services;

public interface IBookingService
{
    Task<BookingDto?> GetCurrentCartAsync(CancellationToken ct = default);
    Task<BookingDto> SelectCartAsync(CartSelectRequest request, CancellationToken ct = default);
    Task ReleaseCartAsync(CancellationToken ct = default);
    Task<BookingDto> CheckoutAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BookingDto>> GetMyBookingsAsync(CancellationToken ct = default);
    Task<BookingDto> CancelBookingAsync(Guid bookingId, CancellationToken ct = default);
    Task ResendEmailsAsync(Guid bookingId, CancellationToken ct = default);
    Task ForceCancelAsync(Guid bookingId, CancellationToken ct = default);
}
