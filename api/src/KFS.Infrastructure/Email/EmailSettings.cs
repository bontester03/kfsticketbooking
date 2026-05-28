namespace KFS.Infrastructure.Email;

public class EmailSettings
{
    public const string SectionName = "Email";

    /// <summary>"Console" (write to disk) or "Smtp" (actually send).</summary>
    public string Provider { get; set; } = "Console";

    public string Host { get; set; } = "smtp.office365.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "King Faisal School";

    /// <summary>Base URL of the parent/student portal, included in welcome/reset emails as a sign-in link.</summary>
    public string PortalUrl { get; set; } = "http://localhost:5173";

    /// <summary>DEV ONLY. Accept an untrusted TLS cert on the SMTP connection — needed when a
    /// corporate proxy intercepts outbound 587. Never enable in production.</summary>
    public bool AcceptInvalidCert { get; set; } = false;
}
