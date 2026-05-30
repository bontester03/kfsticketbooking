using KFS.Domain.Common;
using KFS.Domain.Enums;

namespace KFS.Domain.Entities;

public class ScanLog : BaseEntity
{
    /// <summary>Which event the scan was attempted against. Derived from the
    /// scanner token in the URL; lets us produce per-event scan audit reports.</summary>
    public Guid EventId { get; set; }
    public Event? Event { get; set; }

    public ScannedItemType ScannedItemType { get; set; }
    public Guid? ItemId { get; set; }
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public string? ScannerIp { get; set; }
    public string? DeviceInfo { get; set; }
    public ScanResult Result { get; set; }
}
