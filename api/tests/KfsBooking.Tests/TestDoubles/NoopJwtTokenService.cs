using KfsBooking.Application.Interfaces;
using KfsBooking.Domain.Entities;

namespace KfsBooking.Tests.TestDoubles;

public class NoopJwtTokenService : IJwtTokenService
{
    public (string Token, DateTime ExpiresAt) Generate(User user)
        => ("test-token", DateTime.UtcNow.AddHours(1));
}
