using System.Data;
using KFS.Application.Interfaces;
using KFS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KFS.Infrastructure.Persistence;

public class KfsDbContext : DbContext, IApplicationDbContext
{
    public KfsDbContext(DbContextOptions<KfsDbContext> options) : base(options) { }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingItem> BookingItems => Set<BookingItem>();
    public DbSet<AdminPass> AdminPasses => Set<AdminPass>();
    public DbSet<ScanLog> ScanLogs => Set<ScanLog>();
    public DbSet<ReminderLog> ReminderLogs => Set<ReminderLog>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KfsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public Task<IDbContextTransaction> BeginSerializableTransactionAsync(CancellationToken ct = default)
        => Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
}
