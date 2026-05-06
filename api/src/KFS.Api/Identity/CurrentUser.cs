using System.Security.Claims;
using KFS.Application.Interfaces;
using KFS.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace KFS.Api.Identity;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var raw = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public UserType? UserType
    {
        get
        {
            var typ = Principal?.FindFirstValue("typ");
            return typ switch { "stu" => Domain.Enums.UserType.Student, "adm" => Domain.Enums.UserType.Admin, _ => null };
        }
    }

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
    public bool IsAdmin => UserType == Domain.Enums.UserType.Admin;
    public bool IsStudent => UserType == Domain.Enums.UserType.Student;
}
