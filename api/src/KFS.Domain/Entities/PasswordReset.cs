using KFS.Domain.Common;

namespace KFS.Domain.Entities;

public class PasswordReset : BaseEntity
{
    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool Used { get; set; }
}
