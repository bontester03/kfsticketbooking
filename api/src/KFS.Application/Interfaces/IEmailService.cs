namespace KFS.Application.Interfaces;

public record EmailAttachment(string FileName, string ContentType, byte[] Content);

public record OutgoingEmail(
    string To,
    string Subject,
    string HtmlBody,
    IReadOnlyList<EmailAttachment>? Attachments = null);

public interface IEmailService
{
    Task<string?> SendAsync(OutgoingEmail email, CancellationToken ct = default);
}
