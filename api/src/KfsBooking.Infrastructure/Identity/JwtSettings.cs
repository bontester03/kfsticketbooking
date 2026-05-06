namespace KfsBooking.Infrastructure.Identity;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "kfsbooking";
    public string Audience { get; set; } = "kfsbooking-clients";
    public string Secret { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60 * 8;
}
