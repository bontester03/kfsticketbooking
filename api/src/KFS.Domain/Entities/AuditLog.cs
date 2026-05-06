using KFS.Domain.Common;
using KFS.Domain.Enums;

namespace KFS.Domain.Entities;

public class AuditLog : BaseEntity
{
    public ActorType ActorType { get; set; }
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
