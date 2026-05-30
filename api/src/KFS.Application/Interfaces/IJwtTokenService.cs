using KFS.Domain.Enums;

namespace KFS.Application.Interfaces;

public record JwtTokenPair(string AccessToken, DateTime AccessExpiresAt, string RefreshToken, DateTime RefreshExpiresAt);

public interface IJwtTokenService
{
    /// <summary>Issue a JWT pair. Pass eventId for students so the "eid" claim
    /// scopes all subsequent requests to that event. Pass null for admins.</summary>
    JwtTokenPair Issue(Guid userId, UserType userType, string email, IEnumerable<string> roles, Guid? eventId = null);

    string ValidateAccessTokenAndGetSubject(string token);
    string HashRefreshToken(string refreshToken);
    bool VerifyRefreshToken(string presented, string hashed);
    string NewRefreshTokenString();
}
