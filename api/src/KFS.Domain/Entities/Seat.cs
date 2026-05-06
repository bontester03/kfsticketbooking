using KFS.Domain.Common;

namespace KFS.Domain.Entities;

public class Seat : BaseEntity
{
    public Guid ZoneId { get; set; }
    public Zone? Zone { get; set; }

    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string FullLabel { get; set; } = string.Empty;
}
