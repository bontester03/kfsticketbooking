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
    public Guid? IssuedByAdminId { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}
