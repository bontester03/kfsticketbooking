using KfsBooking.Application.DTOs.Bookings;

namespace KfsBooking.Application.Services;

public interface IBookingService
{
    Task<IReadOnlyList<BookingDto>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BookingDto>> GetMineAsync(CancellationToken ct = default);
    Task<BookingDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<BookingDto> CreateAsync(CreateBookingRequest request, CancellationToken ct = default);
    Task<BookingDto> UpdateStatusAsync(Guid id, UpdateBookingStatusRequest request, CancellationToken ct = default);
    Task CancelAsync(Guid id, CancellationToken ct = default);
}
