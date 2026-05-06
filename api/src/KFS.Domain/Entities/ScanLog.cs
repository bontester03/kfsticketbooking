using KFS.Domain.Common;
using KFS.Domain.Enums;

namespace KFS.Domain.Entities;

public class ScanLog : BaseEntity
{
    public ScannedItemType ScannedItemType { get; set; }
    public Guid? ItemId { get; set; }
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public string? ScannerIp { get; set; }
    public string? DeviceInfo { get; set; }
    public ScanResult Result { get; set; }
}
