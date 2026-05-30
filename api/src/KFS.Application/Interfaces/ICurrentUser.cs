using KFS.Domain.Enums;

namespace KFS.Application.Interfaces;

public interface ICurrentUser
{
    Guid? UserId { get; }
    UserType? UserType { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
    bool IsStudent { get; }

    /// <summary>Event the current student is bound to (from the JWT "eid" claim).
    /// Always null for admins — they pick an event via the URL/query param instead.</summary>
    Guid? EventId { get; }
}
