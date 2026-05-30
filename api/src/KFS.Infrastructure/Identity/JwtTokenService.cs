using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KFS.Application.Interfaces;
using KFS.Domain.Enums;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KFS.Infrastructure.Identity;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;
    public JwtTokenService(IOptions<JwtSettings> options) => _settings = options.Value;

    public JwtTokenPair Issue(Guid userId, UserType userType, string email, IEnumerable<string> roles, Guid? eventId = null)
    {
        var accessExpiry = DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);
        var refreshExpiry = DateTime.UtcNow.AddDays(_settings.RefreshTokenDays);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("typ", userType == UserType.Student ? "stu" : "adm"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (eventId.HasValue) claims.Add(new Claim("eid", eventId.Value.ToString()));
        foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_settings.Issuer, _settings.Audience, claims, expires: accessExpiry, signingCredentials: creds);
        var access = new JwtSecurityTokenHandler().WriteToken(token);
        var refresh = NewRefreshTokenString();

        return new JwtTokenPair(access, accessExpiry, refresh, refreshExpiry);
    }

    public string ValidateAccessTokenAndGetSubject(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = _settings.Issuer, ValidAudience = _settings.Audience,
            IssuerSigningKey = key, ClockSkew = TimeSpan.FromMinutes(1)
        }, out _);
        return principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    }

    public string HashRefreshToken(string refreshToken)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(bytes);
    }

    public bool VerifyRefreshToken(string presented, string hashed)
        => string.Equals(HashRefreshToken(presented), hashed, StringComparison.OrdinalIgnoreCase);

    public string NewRefreshTokenString()
    {
        var bytes = new byte[48];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
