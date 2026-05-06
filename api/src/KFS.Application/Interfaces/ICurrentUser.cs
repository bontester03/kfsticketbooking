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
}
