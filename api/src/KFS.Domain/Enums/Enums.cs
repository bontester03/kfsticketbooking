namespace KFS.Domain.Enums;

public enum BookingStatus
{
    Cart = 0,
    Confirmed = 1,
    Cancelled = 2,
    Expired = 3,
    RebookWindow = 4
}

public enum ZoneCode
{
    // Boys event: VIP split by side (Male/Female)
    VIPAF = 0,
    VIPAM = 1,
    VIPBF = 2,
    VIPBM = 3,

    // Generic admin/display zones — used by both events
    GUEST = 4,
    STAFF = 5,
    MEDIA = 6,
    VVIP = 7,

    // Girls event: single-block VIPs (no side split)
    VIPA = 8,
    VIPB = 9,

    // Boys event: hidden green emergency columns (5 seats × 4 columns)
    EMERG_A_LEFT  = 10,
    EMERG_A_RIGHT = 11,
    EMERG_B_LEFT  = 12,
    EMERG_B_RIGHT = 13,

    // PDF-only pass categories — non-reserved buckets used to track per-type quotas.
    PHOTO        = 14,   // Photographers
    PASSISTANT   = 15,   // Personal assistants
    VISITORS     = 16,   // Visitors / grandmothers row (boys event)
    EMERG_PDF    = 17    // Emergency green-zone PDF passes (boys event)
}

public enum ZoneGroup
{
    None = 0,
    A = 1,
    B = 2
}

public enum ZoneSide
{
    None = 0,
    Female = 1,
    Male = 2
}

/// <summary>
/// Controls who can see and book a Zone. Student-facing views must filter out
/// AdminOnly zones (the green emergency columns) and show DisplayOnly zones
/// as non-bookable (the VVIP area on the map).
/// </summary>
public enum ZoneVisibility
{
    PublicBookable = 0,   // VIP A / VIP B — students see and book
    AdminOnly      = 1,   // Emergency green columns — hidden from students
    DisplayOnly    = 2    // VVIP area on map — visible but not bookable
}

public enum ParentRole
{
    // Boys event: Father + Mother. Girls event: Mother + Grandmother.
    // Index 0 = "first" seat in the pair (Mother).
    // Index 1 = "second" seat in the pair (Father for boys, Grandmother for girls).
    // The Event.PairLabel string drives the user-facing wording.
    Mother      = 0,
    Father      = 1,
    Grandmother = 2
}

public static class ParentRoleLabels
{
    /// <summary>Render a ParentRole for display, swapping "Father" → "Grandmother"
    /// when the event serves girls. Booking items still carry ParentRole.Father
    /// internally for the second seat of the pair; only the user-facing text changes.</summary>
    public static string Label(ParentRole role, EventGender gender) => (role, gender) switch
    {
        (ParentRole.Mother,      _)                      => "Mother",
        (ParentRole.Father,      EventGender.Female)     => "Grandmother",
        (ParentRole.Father,      _)                      => "Father",
        (ParentRole.Grandmother, _)                      => "Grandmother",
        _ => role.ToString()
    };
}

public enum AdminPassType
{
    VVIP             = 0,
    Guest            = 1,
    Staff            = 2,
    Media            = 3,
    Photographer     = 4,
    PersonalAssistant = 5,
    Visitor          = 6,  // Boys event: grandmothers visitor row (PDF only)
    Emergency        = 7   // Boys event: 20 green-zone emergency PDFs
}

public enum ScannedItemType
{
    BookingItem = 0,
    AdminPass = 1
}

public enum ScanResult
{
    Valid = 0,
    AlreadyUsed = 1,
    Invalid = 2,
    Expired = 3
}

public enum ReminderType
{
    Unbooked = 0,
    DayBefore = 1
}

public enum ActorType
{
    Student = 0,
    Admin = 1,
    System = 2
}

public enum AdminRole
{
    SuperAdmin = 0,
    Admin = 1
}

public enum UserType
{
    Student = 0,
    Admin = 1
}

public enum PassOutputFormat
{
    Pdf = 0,
    Zip = 1
}

/// <summary>
/// Identifies which school an event serves. Students are routed to the event
/// whose Gender matches their own (Student.Gender = "Male" -> Boys event).
/// </summary>
public enum EventGender
{
    Male   = 1,   // Boys school event
    Female = 2    // Girls school event
}
