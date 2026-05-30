using KFS.Domain.Common;
using KFS.Domain.Enums;

namespace KFS.Domain.Entities;

public class Zone : BaseEntity
{
    public Guid EventId { get; set; }
    public Event? Event { get; set; }

    public ZoneCode Code { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public ZoneGroup Group { get; set; } = ZoneGroup.None;
    public ZoneSide Side { get; set; } = ZoneSide.None;
    public bool IsReservedSeating { get; set; }
    public int Capacity { get; set; }

    /// <summary>Default PublicBookable. Set AdminOnly for the green emergency columns
    /// (hidden from the student seat map) and DisplayOnly for the VVIP strip
    /// (visible on the map but not bookable).</summary>
    public ZoneVisibility Visibility { get; set; } = ZoneVisibility.PublicBookable;

    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
}
