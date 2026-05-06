using KfsBooking.Domain.Common;
using KfsBooking.Domain.Enums;

namespace KfsBooking.Domain.Entities;

public class Booking : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid AuditoriumId { get; set; }
    public Auditorium? Auditorium { get; set; }

    public string Purpose { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public string? RejectionReason { get; set; }
}
