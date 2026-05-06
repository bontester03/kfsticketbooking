using KfsBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KfsBooking.Infrastructure.Persistence.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> b)
    {
        b.ToTable("bookings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Purpose).HasMaxLength(500).IsRequired();
        b.Property(x => x.RejectionReason).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<int>();

        b.HasOne(x => x.User)
            .WithMany(u => u.Bookings)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Auditorium)
            .WithMany(a => a.Bookings)
            .HasForeignKey(x => x.AuditoriumId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.AuditoriumId, x.StartTime, x.EndTime });
        b.HasIndex(x => x.UserId);
    }
}
