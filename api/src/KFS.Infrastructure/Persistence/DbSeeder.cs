using KFS.Application.Interfaces;
using KFS.Application.Services;
using KFS.Domain.Entities;
using KFS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KFS.Infrastructure.Persistence;

/// <summary>
/// Seeds the database from scratch: one super-admin, two events (Boys + Girls),
/// each with the exact zones / seat counts spec'd in the booking PDF, plus a
/// handful of sample students routed to each event by Gender.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KfsDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        // No migrations yet — EnsureCreated rebuilds the schema fresh on every clean checkout.
        // Once we commit the first migration, switch to db.Database.MigrateAsync() exclusively.
        var migrationsAssembly = db.GetService<IMigrationsAssembly>();
        if (migrationsAssembly.Migrations.Any())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            logger.LogWarning("No EF migrations detected; falling back to EnsureCreated.");
            await db.Database.EnsureCreatedAsync();
        }

        await SeedSuperAdminAsync(db, hasher, config, logger);

        var boys = await SeedEventAsync(db, EventGender.Male, logger);
        var girls = await SeedEventAsync(db, EventGender.Female, logger);

        await SeedSampleStudentsAsync(db, hasher, boys, girls, logger);
    }

    // ---------------- Super admin ----------------

    private static async Task SeedSuperAdminAsync(KfsDbContext db, IPasswordHasher hasher,
        IConfiguration config, ILogger logger)
    {
        if (await db.Admins.AnyAsync()) return;

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
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded super-admin (must-change-password=true).");
    }

    // ---------------- Per-event seed ----------------

    private static async Task<Event> SeedEventAsync(KfsDbContext db, EventGender gender, ILogger logger)
    {
        var existing = await db.Events.FirstOrDefaultAsync(e => e.Gender == gender);
        if (existing is not null) return existing;

        var (name, slug, pairLabel, guestSeats, venue) = gender switch
        {
            EventGender.Male => (
                "KFS Annual Function — Boys",
                "boys",
                "Father & Mother",
                3,
                "KFS Boys Campus Auditorium"),
            EventGender.Female => (
                "KFS Annual Function — Girls",
                "girls",
                "Mother & Grandmother",
                5,
                "KFS Girls Campus Auditorium"),
            _ => throw new InvalidOperationException($"Unsupported event gender: {gender}")
        };

        var ev = new Event
        {
            Name = name,
            Slug = slug,
            Gender = gender,
            PairLabel = pairLabel,
            GuestSeatsPerPass = guestSeats,
            EventDate = DateTime.UtcNow.AddDays(30),
            Venue = venue,
            VenueAddress = "King Faisal School, Riyadh, Saudi Arabia",
            MapLink = "https://maps.google.com/?q=King+Faisal+School+Riyadh",
            IsActive = true,
            BookingOpensAt = DateTime.UtcNow,
            BookingClosesAt = DateTime.UtcNow.AddDays(28),
            CartHoldMinutes = 10,
            CancellationWindowMinutes = 10,
            ScannerToken = Guid.NewGuid().ToString("N")
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        if (gender == EventGender.Male) await SeedBoysZonesAsync(db, ev);
        else                            await SeedGirlsZonesAsync(db, ev);

        logger.LogInformation("Seeded {Name} (slug={Slug}, gender={Gender}).", name, slug, gender);
        return ev;
    }

    // ---------------- Boys event zones ----------------
    // From PDF page 1 diagram:
    //  - VIP A & VIP B: each "5 rows × 2 columns, each column 15 seats" = 150 seats
    //    per VIP block, split into Female (75) + Male (75) sides.
    //  - 4 emergency columns (one each side of A & B), 5 seats each = 20 hidden seats.
    //  - Guest 600, VVIP 100 (display only), Staff 100, Media 100, Visitors capacity unbounded.
    private static async Task SeedBoysZonesAsync(KfsDbContext db, Event ev)
    {
        // VIP blocks — 5 rows, 15 seats per side, both sides per group.
        await CreateReservedZoneAsync(db, ev, ZoneCode.VIPAF, "VIP A — Female",
            ZoneGroup.A, ZoneSide.Female, rows: 5, seatsPerRow: 15);
        await CreateReservedZoneAsync(db, ev, ZoneCode.VIPAM, "VIP A — Male",
            ZoneGroup.A, ZoneSide.Male,   rows: 5, seatsPerRow: 15);
        await CreateReservedZoneAsync(db, ev, ZoneCode.VIPBF, "VIP B — Female",
            ZoneGroup.B, ZoneSide.Female, rows: 5, seatsPerRow: 15);
        await CreateReservedZoneAsync(db, ev, ZoneCode.VIPBM, "VIP B — Male",
            ZoneGroup.B, ZoneSide.Male,   rows: 5, seatsPerRow: 15);

        // Emergency green columns — hidden from students, 5 seats each.
        await CreateReservedZoneAsync(db, ev, ZoneCode.EMERG_A_LEFT,  "Emergency A Left",
            ZoneGroup.A, ZoneSide.Female, rows: 5, seatsPerRow: 1, ZoneVisibility.AdminOnly);
        await CreateReservedZoneAsync(db, ev, ZoneCode.EMERG_A_RIGHT, "Emergency A Right",
            ZoneGroup.A, ZoneSide.Male,   rows: 5, seatsPerRow: 1, ZoneVisibility.AdminOnly);
        await CreateReservedZoneAsync(db, ev, ZoneCode.EMERG_B_LEFT,  "Emergency B Left",
            ZoneGroup.B, ZoneSide.Female, rows: 5, seatsPerRow: 1, ZoneVisibility.AdminOnly);
        await CreateReservedZoneAsync(db, ev, ZoneCode.EMERG_B_RIGHT, "Emergency B Right",
            ZoneGroup.B, ZoneSide.Male,   rows: 5, seatsPerRow: 1, ZoneVisibility.AdminOnly);

        // Non-reserved zones (no seat rows; capacity tracked for QR generation limits).
        await CreateBucketZoneAsync(db, ev, ZoneCode.GUEST, "Guest Zone",        capacity: 600);
        await CreateBucketZoneAsync(db, ev, ZoneCode.VVIP,  "VVIP Zone",         capacity: 100, ZoneVisibility.DisplayOnly);
        await CreateBucketZoneAsync(db, ev, ZoneCode.STAFF, "Staff Zone",        capacity: 100);
        await CreateBucketZoneAsync(db, ev, ZoneCode.MEDIA, "Media Zone",        capacity: 100);

        // PDF-only quotas (boys event — per PDF page 2):
        // 150 Photographers, 150 Personal Assistants, 50 Visitors (grandmothers), 20 Emergency green.
        await CreateBucketZoneAsync(db, ev, ZoneCode.PHOTO,      "Photographers",      capacity: 150);
        await CreateBucketZoneAsync(db, ev, ZoneCode.PASSISTANT, "Personal Assistants", capacity: 150);
        await CreateBucketZoneAsync(db, ev, ZoneCode.VISITORS,   "Visitors",            capacity: 50);
        await CreateBucketZoneAsync(db, ev, ZoneCode.EMERG_PDF,  "Emergency Passes",    capacity: 20);

        await db.SaveChangesAsync();
    }

    // ---------------- Girls event zones ----------------
    // From PDF page 3 diagram:
    //  - VIP A & VIP B: 40 seats each, single block (no Male/Female side split).
    //    Layout: 4 rows × 10 seats — diagram title says 40 seats (the "3 rows × 26"
    //    is the original spec but reconciles to 40 per the title — diagram-first rule).
    //  - Guest 500, VVIP 20, Staff 100, Media 50.
    //  - No emergency green for the girls event (text on PDF page 4 doesn't mention it).
    private static async Task SeedGirlsZonesAsync(KfsDbContext db, Event ev)
    {
        await CreateReservedZoneAsync(db, ev, ZoneCode.VIPA, "VIP A",
            ZoneGroup.A, ZoneSide.None, rows: 4, seatsPerRow: 10);
        await CreateReservedZoneAsync(db, ev, ZoneCode.VIPB, "VIP B",
            ZoneGroup.B, ZoneSide.None, rows: 4, seatsPerRow: 10);

        await CreateBucketZoneAsync(db, ev, ZoneCode.GUEST, "Guest Zone", capacity: 500);
        await CreateBucketZoneAsync(db, ev, ZoneCode.VVIP,  "VVIP Zone",  capacity: 20, ZoneVisibility.DisplayOnly);
        await CreateBucketZoneAsync(db, ev, ZoneCode.STAFF, "Staff Zone", capacity: 100);
        await CreateBucketZoneAsync(db, ev, ZoneCode.MEDIA, "Media Zone", capacity: 50);

        // PDF-only quotas (girls event — per PDF page 4): 75 each. No Visitor / Emergency for girls.
        await CreateBucketZoneAsync(db, ev, ZoneCode.PHOTO,      "Photographers",      capacity: 75);
        await CreateBucketZoneAsync(db, ev, ZoneCode.PASSISTANT, "Personal Assistants", capacity: 75);

        await db.SaveChangesAsync();
    }

    // ---------------- Zone helpers ----------------

    private static async Task CreateReservedZoneAsync(KfsDbContext db, Event ev,
        ZoneCode code, string displayName, ZoneGroup group, ZoneSide side,
        int rows, int seatsPerRow,
        ZoneVisibility visibility = ZoneVisibility.PublicBookable)
    {
        var capacity = rows * seatsPerRow;
        var zone = new Zone
        {
            EventId = ev.Id,
            Code = code,
            DisplayName = displayName,
            Group = group,
            Side = side,
            Capacity = capacity,
            IsReservedSeating = true,
            Visibility = visibility
        };
        db.Zones.Add(zone);
        await db.SaveChangesAsync();

        for (var r = 0; r < rows; r++)
        {
            var rowLabel = ((char)('A' + r)).ToString();   // A, B, C, ...
            for (var n = 1; n <= seatsPerRow; n++)
            {
                db.Seats.Add(new Seat
                {
                    ZoneId = zone.Id,
                    RowLabel = rowLabel,
                    SeatNumber = n,
                    FullLabel = $"{rowLabel}{n}"
                });
            }
        }
    }

    private static Task CreateBucketZoneAsync(KfsDbContext db, Event ev,
        ZoneCode code, string displayName, int capacity,
        ZoneVisibility visibility = ZoneVisibility.PublicBookable)
    {
        // "Bucket" = non-reserved zone with no Seat rows. Capacity is metadata for
        // QR-generation limits (e.g. don't issue more than 100 Staff QRs).
        db.Zones.Add(new Zone
        {
            EventId = ev.Id,
            Code = code,
            DisplayName = displayName,
            Group = ZoneGroup.None,
            Side = ZoneSide.None,
            Capacity = capacity,
            IsReservedSeating = false,
            Visibility = visibility
        });
        return Task.CompletedTask;
    }

    // ---------------- Sample students ----------------

    private static async Task SeedSampleStudentsAsync(KfsDbContext db, IPasswordHasher hasher,
        Event boys, Event girls, ILogger logger)
    {
        if (await db.Students.AnyAsync()) return;

        var samples = new (string First, string Last, string Gender, Event Event)[]
        {
            ("Yousef", "Almutairi", "Male",   boys),
            ("Khalid", "Alharbi",   "Male",   boys),
            ("Abdullah","Alqahtani","Male",   boys),
            ("Safa",   "Albuhairan","Female", girls),
            ("Layan",  "Alqahtani", "Female", girls),
            ("Reem",   "Alotaibi",  "Female", girls)
        };

        var i = 100;
        foreach (var (first, last, gender, ev) in samples)
        {
            var studentNumber = (i++).ToString();
            var email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}@stu.kfs.sch.sa";
            var pwd = StudentService.ComputeInitialPassword(first, studentNumber, dob: null);
            db.Students.Add(new Student
            {
                Email = email,
                FirstName = first,
                LastName = last,
                StudentNumber = studentNumber,
                Gender = gender,
                EventId = ev.Id,
                PasswordHash = hasher.Hash(pwd),
                MustChangePassword = true,
                IsActive = true
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} sample students across both events. " +
            "Passwords follow {{First3Cap}}{{StudentNumber}}.", samples.Length);
    }
}
