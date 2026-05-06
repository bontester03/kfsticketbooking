using KFS.Domain.Common;
using KFS.Domain.Enums;

namespace KFS.Domain.Entities;

public class Admin : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public AdminRole Role { get; set; } = AdminRole.Admin;
    public bool MustChangePassword { get; set; }
    public bool IsActive { get; set; } = true;
}
