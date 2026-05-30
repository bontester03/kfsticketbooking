using KFS.Domain.Common;
using KFS.Domain.Enums;

namespace KFS.Domain.Entities;

public class Student : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>School's roster identifier (e.g. "437079") — used in the initial password.</summary>
    public string? StudentNumber { get; set; }
    /// <summary>Arabic / preferred display name from the school roster.</summary>
    public string? PreferredName { get; set; }

    /// <summary>"Male" routes student to the Boys event; "Female" to Girls.
    /// Required — login is blocked if missing because we can't route them.</summary>
    public string? Gender { get; set; }

    /// <summary>The event this student belongs to. Resolved from Gender at import
    /// time (Male -> Boys event, Female -> Girls event). All booking + seat
    /// queries for this student scope to this EventId.</summary>
    public Guid EventId { get; set; }
    public Event? Event { get; set; }

    /// <summary>DOB is no longer mandatory; current rosters carry StudentNumber instead.</summary>
    public DateTime? DateOfBirth { get; set; }
    public string? GradeOrClass { get; set; }

    /// <summary>VIP group (A or B) pre-assigned by the school. Students may only book seats in
    /// their assigned group. Null means no assignment yet — booking is blocked until set.</summary>
    public ZoneGroup? AssignedGroup { get; set; }

    public string PasswordHash { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
