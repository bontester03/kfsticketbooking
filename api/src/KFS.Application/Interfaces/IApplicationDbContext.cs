using KFS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KFS.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Student> Students { get; }
    DbSet<Admin> Admins { get; }
    DbSet<Event> Events { get; }
    DbSet<Zone> Zones { get; }
    DbSet<Seat> Seats { get; }
    DbSet<Booking> Bookings { get; }
    DbSet<BookingItem> BookingItems { get; }
    DbSet<AdminPass> AdminPasses { get; }
    DbSet<ScanLog> ScanLogs { get; }
    DbSet<ReminderLog> ReminderLogs { get; }
    DbSet<PasswordReset> PasswordResets { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<IDbContextTransaction> BeginSerializableTransactionAsync(CancellationToken ct = default);
}
