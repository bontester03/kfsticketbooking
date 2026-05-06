using KFS.Application.Interfaces;
using KFS.Application.Services;
using KFS.Domain.Entities;
using KFS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KFS.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KfsDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        await db.Database.MigrateAsync();

        if (!await db.Admins.AnyAsync())
        {
            var pwd = config.GetValue<string>("Auth:SuperAdminPassword") ?? "Admin@123";
            db.Admins.Add(new Admin
            {
                Email = "admin@kfs.sch.sa",
                FullName = "KFS Super Admin",
                PasswordHash = hasher.Hash(pwd),
                Role = AdminRole.SuperAdmin,
                MustChangePassword = true,
                IsActive = true
            });
            logger.LogInformation("Seeded super-admin (must-change-password=true).");
        }

        var ev = await db.Events.FirstOrDefaultAsync();
        if (ev is null)
        {
            ev = new Event
            {
                Name = "KFS Annual Event",
                EventDate = DateTime.UtcNow.AddDays(30),
                Venue = "KFS School Auditorium",
                VenueAddress = "King Fahd Street, Riyadh, Saudi Arabia",
                MapLink = "https://maps.google.com/?q=KFS+School+Riyadh",
                IsActive = true,
                BookingOpensAt = DateTime.UtcNow,
                BookingClosesAt = DateTime.UtcNow.AddDays(28),
                CartHoldMinutes = 10,
                CancellationWindowMinutes = 10,
                ScannerToken = Guid.NewGuid().ToString("N")
            };
            db.Events.Add(ev);
            await db.SaveChangesAsync();

            // Zones — 4 VIP + 4 admin-issued.
            var vipZones = new[]
            {
                (ZoneCode.VIPAF, ZoneGroup.A, ZoneSide.Female, "VIP A — Female",  76, true),
                (ZoneCode.VIPAM, ZoneGroup.A, ZoneSide.Male,   "VIP A — Male",    76, true),
                (ZoneCode.VIPBF, ZoneGroup.B, ZoneSide.Female, "VIP B — Female",  76, true),
                (ZoneCode.VIPBM, ZoneGroup.B, ZoneSide.Male,   "VIP B — Male",    76, true),
                (ZoneCode.GUEST, ZoneGroup.None, ZoneSide.None, "Guest Zone",     600, false),
                (ZoneCode.STAFF, ZoneGroup.None, ZoneSide.None, "Staff Zone",     100, false),
                (ZoneCode.MEDIA, ZoneGroup.None, ZoneSide.None, "Media Zone",     100, false),
                (ZoneCode.VVIP,  ZoneGroup.None, ZoneSide.None, "VVIP Zone",      100, false)
            };

            foreach (var (code, group, side, name, capacity, reserved) in vipZones)
            {
                var z = new Zone
                {
                    EventId = ev.Id, Code = code, DisplayName = name,
                    Group = group, Side = side,
                    Capacity = capacity, IsReservedSeating = reserved
                };
                db.Zones.Add(z);
                await db.SaveChangesAsync();

                if (reserved)
                {
                    var rows = new[] { "A", "B", "C", "D" };
                    foreach (var row in rows)
                    {
                        for (var n = 1; n <= 19; n++)
                        {
                            db.Seats.Add(new Seat
                            {
                                ZoneId = z.Id,
                                RowLabel = row,
                                SeatNumber = n,
                                FullLabel = $"{row}{n}"
                            });
                        }
                    }
                }
            }
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded event with 8 zones and 304 VIP seats.");
        }

        if (!await db.Students.AnyAsync())
        {
            var samples = new[]
            {
                ("Safa", "Albuhairan", new DateTime(2010, 3, 15)),
                ("Layan", "Alqahtani",  new DateTime(2009, 6, 22)),
                ("Yousef", "Almutairi",  new DateTime(2010, 11, 4)),
                ("Reem",  "Alotaibi",   new DateTime(2009, 1, 30)),
                ("Khalid","Alharbi",    new DateTime(2010, 8, 12))
            };
            foreach (var (first, last, dob) in samples)
            {
                var email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}@stu.kfs.sch.sa";
                var pwd = StudentService.ComputeInitialPassword(first, dob);
                db.Students.Add(new Student
                {
                    Email = email, FirstName = first, LastName = last, DateOfBirth = dob,
                    PasswordHash = hasher.Hash(pwd), MustChangePassword = true, IsActive = true
                });
            }
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded 5 sample students. Initial passwords follow {{First3LetterCapitalised}}{{ddMMyyyy}} format.");
        }
    }
}
