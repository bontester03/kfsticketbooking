using KfsBooking.Application.Interfaces;
using KfsBooking.Domain.Entities;
using KfsBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KfsBooking.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        await db.Database.MigrateAsync();

        if (!await db.Users.AnyAsync())
        {
            logger.LogInformation("Seeding default users...");
            db.Users.AddRange(
                new User
                {
                    FullName = "KFS Admin",
                    Email = "admin@kfs.local",
                    PasswordHash = hasher.Hash("Admin@123"),
                    Role = UserRole.Admin
                },
                new User
                {
                    FullName = "Jane Teacher",
                    Email = "teacher@kfs.local",
                    PasswordHash = hasher.Hash("Teacher@123"),
                    Role = UserRole.Teacher
                },
                new User
                {
                    FullName = "John Student",
                    Email = "student@kfs.local",
                    PasswordHash = hasher.Hash("Student@123"),
                    Role = UserRole.Student
                });
        }

        if (!await db.Auditoriums.AnyAsync())
        {
            logger.LogInformation("Seeding auditoriums...");
            db.Auditoriums.AddRange(
                new Auditorium
                {
                    Name = "Main Hall",
                    Location = "Block A, Ground Floor",
                    Capacity = 500,
                    Description = "Main school auditorium for assemblies and events."
                },
                new Auditorium
                {
                    Name = "Mini Auditorium",
                    Location = "Block B, 1st Floor",
                    Capacity = 120,
                    Description = "Mid-sized hall ideal for seminars and workshops."
                },
                new Auditorium
                {
                    Name = "Conference Room",
                    Location = "Block C, 2nd Floor",
                    Capacity = 40,
                    Description = "For staff meetings and small gatherings."
                });
        }

        await db.SaveChangesAsync();
    }
}
