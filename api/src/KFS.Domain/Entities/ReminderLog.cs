using KFS.Domain.Common;
using KFS.Domain.Enums;

namespace KFS.Domain.Entities;

public class ReminderLog : BaseEntity
{
    public Guid EventId { get; set; }
    public Event? Event { get; set; }

    public Guid? StudentId { get; set; }
    public Student? Student { get; set; }

    public ReminderType Type { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string? EmailMessageId { get; set; }
}
