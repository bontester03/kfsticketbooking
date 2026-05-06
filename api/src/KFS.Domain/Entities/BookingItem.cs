using KFS.Domain.Common;
using KFS.Domain.Enums;

namespace KFS.Domain.Entities;

public class BookingItem : BaseEntity
{
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }

    public Guid ZoneId { get; set; }
    public Zone? Zone { get; set; }

    public Guid SeatId { get; set; }
    public Seat? Seat { get; set; }

    public ParentRole ParentRole { get; set; }

    public string TicketNumber { get; set; } = string.Empty;
    public string QrCodePayload { get; set; } = string.Empty;
    public string? QrCodeImageUrl { get; set; }

    public bool EmailSent { get; set; }
    public DateTime? EmailSentAt { get; set; }
    public DateTime HoldExpiresAt { get; set; }
}
