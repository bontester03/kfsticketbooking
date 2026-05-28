namespace KFS.Application.Interfaces;

// ContentId set → embedded inline (referenced from HTML via cid:<ContentId>); otherwise a regular attachment.
public record EmailAttachment(string FileName, string ContentType, byte[] Content, string? ContentId = null);

public record OutgoingEmail(
    string To,
    string Subject,
    string HtmlBody,
    IReadOnlyList<EmailAttachment>? Attachments = null);

public interface IEmailService
{
    Task<string?> SendAsync(OutgoingEmail email, CancellationToken ct = default);
}
