using System.Net;
using System.Net.Mail;
using KFS.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KFS.Infrastructure.Email;

/// Sends real email over SMTP (Microsoft 365 / Office 365: smtp.office365.com:587, STARTTLS).
/// Requires SMTP AUTH to be enabled for the mailbox in the M365 admin centre.
public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _s;
    private readonly ILogger<SmtpEmailService> _log;

    public SmtpEmailService(IOptions<EmailSettings> options, ILogger<SmtpEmailService> log)
    {
        _s = options.Value; _log = log;
    }

    public async Task<string?> SendAsync(OutgoingEmail email, CancellationToken ct = default)
    {
        var fromAddress = string.IsNullOrWhiteSpace(_s.FromAddress) ? _s.Username : _s.FromAddress;

        using var msg = new MailMessage
        {
            From = new MailAddress(fromAddress, _s.FromName),
            Subject = email.Subject
        };
        msg.To.Add(email.To);

        var streams = new List<MemoryStream>();
        var inlineImages = email.Attachments?.Where(a => !string.IsNullOrEmpty(a.ContentId)).ToList()
            ?? new List<EmailAttachment>();
        var fileAttachments = email.Attachments?.Where(a => string.IsNullOrEmpty(a.ContentId)).ToList()
            ?? new List<EmailAttachment>();

        if (inlineImages.Count > 0)
        {
            // Use an AlternateView so inline <img src="cid:..."> resolves against LinkedResources.
            var view = AlternateView.CreateAlternateViewFromString(email.HtmlBody, null, "text/html");
            foreach (var img in inlineImages)
            {
                var ms = new MemoryStream(img.Content); streams.Add(ms);
                var res = new LinkedResource(ms, img.ContentType) { ContentId = img.ContentId };
                view.LinkedResources.Add(res);
            }
            msg.AlternateViews.Add(view);
        }
        else
        {
            msg.Body = email.HtmlBody;
            msg.IsBodyHtml = true;
        }

        foreach (var a in fileAttachments)
        {
            var ms = new MemoryStream(a.Content); streams.Add(ms);
            msg.Attachments.Add(new Attachment(ms, a.FileName, a.ContentType));
        }

        try
        {
            // DEV ONLY: trust the corporate proxy's intercepting cert when explicitly allowed.
            if (_s.AcceptInvalidCert)
                ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;

            using var client = new SmtpClient(_s.Host, _s.Port)
            {
                EnableSsl = true,                      // STARTTLS on 587
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Credentials = new NetworkCredential(_s.Username, _s.Password)
            };
            await client.SendMailAsync(msg, ct);
            _log.LogInformation("Email sent to {To} — {Subject}", email.To, email.Subject);
            return $"smtp-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "SMTP send to {To} failed via {Host}:{Port} as {User}",
                email.To, _s.Host, _s.Port, _s.Username);
            throw;
        }
        finally
        {
            foreach (var s in streams) s.Dispose();
        }
    }
}
