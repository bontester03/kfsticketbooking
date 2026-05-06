using System.Text;
using KFS.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace KFS.Infrastructure.Email;

/// Writes rendered email HTML + attachments to the local logs/email directory.
/// Lets developers see exactly what would have been sent without depending on a live SMTP/SendGrid account.
public class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _log;
    private readonly string _outputDir;

    public ConsoleEmailService(ILogger<ConsoleEmailService> log)
    {
        _log = log;
        _outputDir = Path.Combine(AppContext.BaseDirectory, "logs", "email");
        Directory.CreateDirectory(_outputDir);
    }

    public async Task<string?> SendAsync(OutgoingEmail email, CancellationToken ct = default)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmss");
        var safeTo = email.To.Replace("@", "_at_").Replace("/", "_");
        var name = $"{stamp}-{safeTo}";
        var dir = Path.Combine(_outputDir, name);
        Directory.CreateDirectory(dir);

        var meta = new StringBuilder()
            .AppendLine($"To: {email.To}")
            .AppendLine($"Subject: {email.Subject}")
            .AppendLine($"At: {DateTime.UtcNow:o}");
        await File.WriteAllTextAsync(Path.Combine(dir, "meta.txt"), meta.ToString(), ct);
        await File.WriteAllTextAsync(Path.Combine(dir, "body.html"), email.HtmlBody, ct);

        if (email.Attachments != null)
            foreach (var att in email.Attachments)
                await File.WriteAllBytesAsync(Path.Combine(dir, att.FileName), att.Content, ct);

        _log.LogInformation("Email rendered to disk: {Dir} (To: {To}, Subject: {Subject})", dir, email.To, email.Subject);
        return name;
    }
}
