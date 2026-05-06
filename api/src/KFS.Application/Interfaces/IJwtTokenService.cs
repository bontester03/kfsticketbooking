using KFS.Domain.Enums;

namespace KFS.Application.Interfaces;

public record JwtTokenPair(string AccessToken, DateTime AccessExpiresAt, string RefreshToken, DateTime RefreshExpiresAt);

public interface IJwtTokenService
{
    JwtTokenPair Issue(Guid userId, UserType userType, string email, IEnumerable<string> roles);
    string ValidateAccessTokenAndGetSubject(string token);
    string HashRefreshToken(string refreshToken);
    bool VerifyRefreshToken(string presented, string hashed);
    string NewRefreshTokenString();
}
