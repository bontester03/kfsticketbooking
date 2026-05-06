using KfsBooking.Application.Interfaces;
using KfsBooking.Domain.Enums;

namespace KfsBooking.Tests.TestDoubles;

public class StubCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public UserRole? Role { get; set; }
    public bool IsAuthenticated => UserId.HasValue;
}
