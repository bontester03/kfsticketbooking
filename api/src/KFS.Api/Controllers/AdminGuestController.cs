using KFS.Application.Common.Exceptions;
using KFS.Application.DTOs.Passes;
using KFS.Application.Interfaces;
using KFS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KFS.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/guest")]
public class AdminGuestController : ControllerBase
{
    private readonly IGuestPassService _guest;
    private readonly ICurrentUser _currentUser;

    public AdminGuestController(IGuestPassService guest, ICurrentUser currentUser)
    {
        _guest = guest; _currentUser = currentUser;
    }

    [HttpGet("analytics")]
    public Task<GuestAnalyticsDto> Analytics(CancellationToken ct) => _guest.GetAnalyticsAsync(ct);

    // Students with a flag for whether they already hold a guest ticket.
    [HttpGet("students")]
    public Task<IReadOnlyList<GuestEligibleStudentDto>> Students([FromQuery] string? search, CancellationToken ct)
        => _guest.ListStudentsAsync(search, ct);

    // Issue the guest ticket to a child on their behalf (fails if they already have one).
    [HttpPost("issue")]
    public Task<GuestPassDto> Issue([FromBody] IssueGuestToStudentRequest request, CancellationToken ct)
    {
        var adminId = _currentUser.UserId
            ?? throw new AppException("unauthorized", "Not signed in.", 401);
        return _guest.BookForStudentAsync(request.StudentId, adminId, request.IssuedToName, ct);
    }
}
