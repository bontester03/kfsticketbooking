using KFS.Domain.Common;

namespace KFS.Domain.Entities;

public class Event : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Venue { get; set; } = string.Empty;
    public string VenueAddress { get; set; } = string.Empty;
    public string? MapLink { get; set; }

    public bool IsActive { get; set; }
    public DateTime BookingOpensAt { get; set; }
    public DateTime BookingClosesAt { get; set; }

    public int CartHoldMinutes { get; set; } = 10;
    public int CancellationWindowMinutes { get; set; } = 10;

    public bool ReminderDayBeforeSent { get; set; }
    public string? ReminderNoteFromAdmin { get; set; }

    public string ScannerToken { get; set; } = string.Empty;

    public ICollection<Zone> Zones { get; set; } = new List<Zone>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<AdminPass> AdminPasses { get; set; } = new List<AdminPass>();
}
