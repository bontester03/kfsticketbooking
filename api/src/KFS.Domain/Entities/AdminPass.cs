using KFS.Domain.Common;
using KFS.Domain.Enums;

namespace KFS.Domain.Entities;

public class AdminPass : BaseEntity
{
    public Guid EventId { get; set; }
    public Event? Event { get; set; }

    public AdminPassType Type { get; set; }
    public Guid BatchId { get; set; }
    public int SequenceNumber { get; set; }

    public string TicketNumber { get; set; } = string.Empty;
    public string QrCodePayload { get; set; } = string.Empty;
    public string? QrCodeImageUrl { get; set; }

    public int SeatsCount { get; set; } = 1;
    public string? IssuedToName { get; set; }
    /// <summary>Set when the pass was generated from a staff/photographer/PA roster —
    /// drives the per-pass "your pass" email + the bulk Send-emails action.</summary>
    public string? IssuedToEmail { get; set; }
    /// <summary>Did the holder receive their QR by email? Flipped true after a successful
    /// SmtpClient.SendAsync; admins use the per-pass Resend button if it's still false.</summary>
    public bool EmailSent { get; set; }
    public DateTime? EmailSentAt { get; set; }
    public Guid? IssuedByAdminId { get; set; }

    // Set when a pass is tied to a specific child: a student self-books a Guest ticket
    // (IssuedByAdminId == null) or an admin issues one to that child (IssuedByAdminId != null).
    // Null for the general admin-issued pool (VVIP/Staff/Media and unassigned Guest passes).
    public Guid? StudentId { get; set; }
    public Student? Student { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}
