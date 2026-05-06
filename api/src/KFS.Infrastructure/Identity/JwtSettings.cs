namespace KFS.Infrastructure.Identity;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "kfs";
    public string Audience { get; set; } = "kfs-clients";
    public string Secret { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}

public class QrSettings
{
    public const string SectionName = "Qr";
    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "kfs-qr";
}
