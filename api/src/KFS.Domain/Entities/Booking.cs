using KFS.Domain.Common;
using KFS.Domain.Enums;

namespace KFS.Domain.Entities;

public class Booking : BaseEntity
{
    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    public Guid EventId { get; set; }
    public Event? Event { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Cart;
    public ZoneGroup GroupChosen { get; set; }

    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? RebookWindowExpiresAt { get; set; }

    public ICollection<BookingItem> Items { get; set; } = new List<BookingItem>();
}
