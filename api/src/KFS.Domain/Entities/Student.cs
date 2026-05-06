using KFS.Domain.Common;

namespace KFS.Domain.Entities;

public class Student : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string? GradeOrClass { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
