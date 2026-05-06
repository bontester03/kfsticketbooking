using KfsBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KfsBooking.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Auditorium> Auditoriums { get; }
    DbSet<Booking> Bookings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
