using FluentAssertions;
using KfsBooking.Application.Common.Exceptions;
using KfsBooking.Application.DTOs.Bookings;
using KfsBooking.Application.Services;
using KfsBooking.Domain.Entities;
using KfsBooking.Domain.Enums;
using KfsBooking.Infrastructure.Persistence;
using KfsBooking.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace KfsBooking.Tests.Services;

public class BookingServiceTests
{
    private static ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static (ApplicationDbContext db, Guid userId, Guid auditoriumId) Seeded()
    {
        var db = CreateDb();
        var user = new User { FullName = "Bob", Email = "bob@kfs.local", PasswordHash = "x", Role = UserRole.Student };
        var hall = new Auditorium { Name = "Main Hall", Location = "A1", Capacity = 100 };
        db.Users.Add(user);
        db.Auditoriums.Add(hall);
        db.SaveChanges();
        return (db, user.Id, hall.Id);
    }

    [Fact]
    public async Task Create_Booking_Succeeds_When_No_Conflict()
    {
        var (db, userId, hallId) = Seeded();
        var current = new StubCurrentUser { UserId = userId, Role = UserRole.Student };
        var svc = new BookingService(db, current);

        var start = DateTime.UtcNow.AddHours(1);
        var booking = await svc.CreateAsync(new CreateBookingRequest(hallId, "Drama Club", start, start.AddHours(2)));

        booking.Status.Should().Be(BookingStatus.Pending);
        booking.AuditoriumId.Should().Be(hallId);
    }

    [Fact]
    public async Task Create_Booking_Conflict_Throws()
    {
        var (db, userId, hallId) = Seeded();
        var current = new StubCurrentUser { UserId = userId, Role = UserRole.Student };
        var svc = new BookingService(db, current);

        var start = DateTime.UtcNow.AddHours(1);
        await svc.CreateAsync(new CreateBookingRequest(hallId, "Drama Club", start, start.AddHours(2)));

        var act = () => svc.CreateAsync(new CreateBookingRequest(hallId, "Music Club", start.AddMinutes(30), start.AddHours(3)));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Cancel_Other_User_Booking_Without_Admin_Throws()
    {
        var (db, ownerId, hallId) = Seeded();
        var owner = new StubCurrentUser { UserId = ownerId, Role = UserRole.Student };
        var svc = new BookingService(db, owner);
        var start = DateTime.UtcNow.AddHours(1);
        var booking = await svc.CreateAsync(new CreateBookingRequest(hallId, "Drama Club", start, start.AddHours(2)));

        var intruder = new StubCurrentUser { UserId = Guid.NewGuid(), Role = UserRole.Student };
        var intruderSvc = new BookingService(db, intruder);

        var act = () => intruderSvc.CancelAsync(booking.Id);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
