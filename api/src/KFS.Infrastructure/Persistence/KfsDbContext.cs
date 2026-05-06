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
        // citext: case-insensitive emails. pgcrypto: gen_random_uuid() (we still generate IDs in
        // app code, but having the extension available means future migrations can use it).
        // pg_trgm: enables trigram-style fuzzy search if we add admin search down the line.
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KfsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public Task<IDbContextTransaction> BeginSerializableTransactionAsync(CancellationToken ct = default)
        => Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
}
