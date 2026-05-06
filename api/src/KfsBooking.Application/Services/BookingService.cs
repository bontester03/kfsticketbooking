using KfsBooking.Application.Common.Exceptions;
using KfsBooking.Application.DTOs.Bookings;
using KfsBooking.Application.Interfaces;
using KfsBooking.Domain.Entities;
using KfsBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KfsBooking.Application.Services;

public class BookingService : IBookingService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public BookingService(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public Task<IReadOnlyList<BookingDto>> GetAllAsync(CancellationToken ct = default)
        => QueryAsync(_ => true, ct);

    public Task<IReadOnlyList<BookingDto>> GetMineAsync(CancellationToken ct = default)
    {
        var userId = RequireUserId();
        return QueryAsync(b => b.UserId == userId, ct);
    }

    public async Task<BookingDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var b = await _db.Bookings
            .Include(x => x.User)
            .Include(x => x.Auditorium)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(nameof(Booking), id);
        return Map(b);
    }

    public async Task<BookingDto> CreateAsync(CreateBookingRequest request, CancellationToken ct = default)
    {
        var userId = RequireUserId();

        if (request.EndTime <= request.StartTime)
            throw new AppException("End time must be after start time.");
        if (request.StartTime < DateTime.UtcNow.AddMinutes(-1))
            throw new AppException("Start time cannot be in the past.");

        var auditorium = await _db.Auditoriums.FindAsync(new object?[] { request.AuditoriumId }, ct)
                         ?? throw new NotFoundException(nameof(Auditorium), request.AuditoriumId);
        if (!auditorium.IsActive) throw new AppException("Auditorium is not available.");

        var hasConflict = await _db.Bookings.AnyAsync(b =>
            b.AuditoriumId == request.AuditoriumId &&
            b.Status != BookingStatus.Rejected &&
            b.Status != BookingStatus.Cancelled &&
            b.StartTime < request.EndTime &&
            request.StartTime < b.EndTime, ct);
        if (hasConflict) throw new ConflictException("Auditorium is already booked for the selected time.");

        var booking = new Booking
        {
            UserId = userId,
            AuditoriumId = request.AuditoriumId,
            Purpose = request.Purpose.Trim(),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = BookingStatus.Pending
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(booking.Id, ct);
    }

    public async Task<BookingDto> UpdateStatusAsync(Guid id, UpdateBookingStatusRequest request, CancellationToken ct = default)
    {
        var b = await _db.Bookings.FindAsync(new object?[] { id }, ct)
                ?? throw new NotFoundException(nameof(Booking), id);

        b.Status = request.Status;
        b.RejectionReason = request.Status == BookingStatus.Rejected ? request.RejectionReason : null;
        b.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task CancelAsync(Guid id, CancellationToken ct = default)
    {
        var userId = RequireUserId();
        var b = await _db.Bookings.FindAsync(new object?[] { id }, ct)
                ?? throw new NotFoundException(nameof(Booking), id);

        if (b.UserId != userId && _currentUser.Role != UserRole.Admin)
            throw new UnauthorizedException("You can only cancel your own bookings.");

        b.Status = BookingStatus.Cancelled;
        b.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<BookingDto>> QueryAsync(
        System.Linq.Expressions.Expression<Func<Booking, bool>> predicate,
        CancellationToken ct)
    {
        return await _db.Bookings
            .Where(predicate)
            .OrderByDescending(b => b.StartTime)
            .Select(b => new BookingDto(
                b.Id,
                b.UserId,
                b.User != null ? b.User.FullName : string.Empty,
                b.AuditoriumId,
                b.Auditorium != null ? b.Auditorium.Name : string.Empty,
                b.Purpose,
                b.StartTime,
                b.EndTime,
                b.Status,
                b.RejectionReason,
                b.CreatedAt))
            .ToListAsync(ct);
    }

    private static BookingDto Map(Booking b) => new(
        b.Id,
        b.UserId,
        b.User?.FullName ?? string.Empty,
        b.AuditoriumId,
        b.Auditorium?.Name ?? string.Empty,
        b.Purpose,
        b.StartTime,
        b.EndTime,
        b.Status,
        b.RejectionReason,
        b.CreatedAt);

    private Guid RequireUserId() =>
        _currentUser.UserId ?? throw new UnauthorizedException("Authentication required.");
}
