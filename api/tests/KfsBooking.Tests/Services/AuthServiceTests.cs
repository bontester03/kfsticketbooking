using FluentAssertions;
using KfsBooking.Application.Common.Exceptions;
using KfsBooking.Application.DTOs.Auth;
using KfsBooking.Application.Services;
using KfsBooking.Domain.Enums;
using KfsBooking.Infrastructure.Identity;
using KfsBooking.Infrastructure.Persistence;
using KfsBooking.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace KfsBooking.Tests.Services;

public class AuthServiceTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Register_Creates_User_And_Returns_Token()
    {
        using var db = CreateDb();
        var svc = new AuthService(db, new NoopJwtTokenService(), new PasswordHasher());

        var resp = await svc.RegisterAsync(new RegisterRequest("Alice", "alice@kfs.local", "Password1!"));

        resp.Email.Should().Be("alice@kfs.local");
        resp.Role.Should().Be(UserRole.Student);
        resp.Token.Should().NotBeNullOrEmpty();
        (await db.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Register_Duplicate_Email_Throws_Conflict()
    {
        using var db = CreateDb();
        var svc = new AuthService(db, new NoopJwtTokenService(), new PasswordHasher());

        await svc.RegisterAsync(new RegisterRequest("Alice", "alice@kfs.local", "Password1!"));
        var act = () => svc.RegisterAsync(new RegisterRequest("Alice", "ALICE@kfs.local", "Password1!"));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Login_Bad_Password_Throws_Unauthorized()
    {
        using var db = CreateDb();
        var svc = new AuthService(db, new NoopJwtTokenService(), new PasswordHasher());
        await svc.RegisterAsync(new RegisterRequest("Alice", "alice@kfs.local", "Password1!"));

        var act = () => svc.LoginAsync(new LoginRequest("alice@kfs.local", "WrongPass"));

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
