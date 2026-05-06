using KFS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KFS.Infrastructure.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> b)
    {
        b.ToTable("Students");
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(180).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.FirstName).HasMaxLength(120).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(120).IsRequired();
        b.Property(x => x.GradeOrClass).HasMaxLength(60);
        b.Property(x => x.PasswordHash).IsRequired();
    }
}

public class AdminConfiguration : IEntityTypeConfiguration<Admin>
{
    public void Configure(EntityTypeBuilder<Admin> b)
    {
        b.ToTable("Admins");
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(180).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.FullName).HasMaxLength(160).IsRequired();
        b.Property(x => x.PasswordHash).IsRequired();
        b.Property(x => x.Role).HasConversion<int>();
    }
}

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> b)
    {
        b.ToTable("Events");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        b.Property(x => x.Venue).HasMaxLength(160).IsRequired();
        b.Property(x => x.VenueAddress).HasMaxLength(280).IsRequired();
        b.Property(x => x.MapLink).HasMaxLength(500);
        b.Property(x => x.ReminderNoteFromAdmin).HasMaxLength(2000);
        b.Property(x => x.ScannerToken).HasMaxLength(120).IsRequired();
        b.HasIndex(x => x.EventDate);
    }
}

public class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> b)
    {
        b.ToTable("Zones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasConversion<int>();
        b.Property(x => x.Group).HasConversion<int>();
        b.Property(x => x.Side).HasConversion<int>();
        b.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();

        b.HasOne(x => x.Event).WithMany(e => e.Zones).HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.EventId, x.Code }).IsUnique();
    }
}

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> b)
    {
        b.ToTable("Seats");
        b.HasKey(x => x.Id);
        b.Property(x => x.RowLabel).HasMaxLength(4).IsRequired();
        b.Property(x => x.FullLabel).HasMaxLength(20).IsRequired();
        b.HasOne(x => x.Zone).WithMany(z => z.Seats).HasForeignKey(x => x.ZoneId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ZoneId, x.RowLabel, x.SeatNumber }).IsUnique();
    }
}

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> b)
    {
        b.ToTable("Bookings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.GroupChosen).HasConversion<int>();

        b.HasOne(x => x.Student).WithMany(s => s.Bookings).HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Event).WithMany(e => e.Bookings).HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.StudentId, x.Status });
        b.HasIndex(x => new { x.EventId, x.Status });
    }
}

public class BookingItemConfiguration : IEntityTypeConfiguration<BookingItem>
{
    public void Configure(EntityTypeBuilder<BookingItem> b)
    {
        b.ToTable("BookingItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.ParentRole).HasConversion<int>();
        b.Property(x => x.TicketNumber).HasMaxLength(60);
        b.Property(x => x.QrCodePayload).HasMaxLength(2000);
        b.Property(x => x.QrCodeImageUrl).HasMaxLength(500);

        b.HasOne(x => x.Booking).WithMany(bk => bk.Items).HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Seat).WithMany().HasForeignKey(x => x.SeatId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.HoldExpiresAt);
        b.HasIndex(x => new { x.BookingId, x.ParentRole }).IsUnique();
        // QrCodePayload is nullable until checkout; Postgres unique indexes treat NULLs as
        // distinct, so multiple Cart rows can coexist before checkout fills the value in.
        b.HasIndex(x => x.QrCodePayload).IsUnique();
    }
}

public class AdminPassConfiguration : IEntityTypeConfiguration<AdminPass>
{
    public void Configure(EntityTypeBuilder<AdminPass> b)
    {
        b.ToTable("AdminPasses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.TicketNumber).HasMaxLength(60).IsRequired();
        b.Property(x => x.QrCodePayload).HasMaxLength(2000).IsRequired();
        b.Property(x => x.QrCodeImageUrl).HasMaxLength(500);
        b.Property(x => x.IssuedToName).HasMaxLength(180);

        b.HasOne(x => x.Event).WithMany(e => e.AdminPasses).HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.BatchId, x.SequenceNumber }).IsUnique();
        b.HasIndex(x => x.QrCodePayload).IsUnique();
    }
}

public class ScanLogConfiguration : IEntityTypeConfiguration<ScanLog>
{
    public void Configure(EntityTypeBuilder<ScanLog> b)
    {
        b.ToTable("ScanLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.ScannedItemType).HasConversion<int>();
        b.Property(x => x.Result).HasConversion<int>();
        b.Property(x => x.ScannerIp).HasMaxLength(64);
        b.Property(x => x.DeviceInfo).HasMaxLength(500);
        b.HasIndex(x => x.ScannedAt);
        b.HasIndex(x => new { x.ScannedItemType, x.ItemId });
    }
}

public class ReminderLogConfiguration : IEntityTypeConfiguration<ReminderLog>
{
    public void Configure(EntityTypeBuilder<ReminderLog> b)
    {
        b.ToTable("ReminderLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.EmailMessageId).HasMaxLength(180);
        b.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PasswordResetConfiguration : IEntityTypeConfiguration<PasswordReset>
{
    public void Configure(EntityTypeBuilder<PasswordReset> b)
    {
        b.ToTable("PasswordResets");
        b.HasKey(x => x.Id);
        b.Property(x => x.Token).HasMaxLength(120).IsRequired();
        b.HasIndex(x => x.Token).IsUnique();
        b.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.ActorType).HasConversion<int>();
        b.Property(x => x.Action).HasMaxLength(120).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(120);
        b.Property(x => x.MetadataJson).HasMaxLength(4000);
        b.HasIndex(x => x.Timestamp);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserType).HasConversion<int>();
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.UserId, x.UserType });
    }
}
