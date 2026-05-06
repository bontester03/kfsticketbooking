using KfsBooking.Domain.Entities;

namespace KfsBooking.Application.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) Generate(User user);
}
