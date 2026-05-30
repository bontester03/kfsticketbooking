using KFS.Domain.Common;
using KFS.Domain.Enums;

namespace KFS.Domain.Entities;

public class Event : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-safe identifier — "boys" or "girls". Used in admin routes:
    /// /admin/{slug}/dashboard. Unique across events.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Which school this event serves. Students are auto-routed to the
    /// event matching their Gender field.</summary>
    public EventGender Gender { get; set; }

    /// <summary>User-facing label for the parent booking pair: "Father & Mother"
    /// for boys, "Mother & Grandmother" for girls. Surfaces in the booking UI
    /// and the confirmation email.</summary>
    public string PairLabel { get; set; } = "Father & Mother";

    /// <summary>Number of seats covered by one student-issued Guest QR. Boys=3,
    /// girls=5. Used by GuestPassService.SeatsCount.</summary>
    public int GuestSeatsPerPass { get; set; } = 3;

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
    public ICollection<Student> Students { get; set; } = new List<Student>();
}
