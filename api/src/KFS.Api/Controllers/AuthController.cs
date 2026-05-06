using KFS.Application.DTOs.Auth;
using KFS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KFS.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("login")]
    public Task<AuthResponse> StudentLogin([FromBody] LoginRequest request, CancellationToken ct)
        => _auth.StudentLoginAsync(request, ct);

    [HttpPost("admin/login")]
    public Task<AuthResponse> AdminLogin([FromBody] LoginRequest request, CancellationToken ct)
        => _auth.AdminLoginAsync(request, ct);

    [HttpPost("refresh")]
    public Task<AuthResponse> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
        => _auth.RefreshAsync(request, ct);

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await _auth.ChangePasswordAsync(request, ct);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> Forgot([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _auth.ForgotPasswordAsync(request, ct);
        return NoContent();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> Reset([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await _auth.ResetPasswordAsync(request, ct);
        return NoContent();
    }
}
