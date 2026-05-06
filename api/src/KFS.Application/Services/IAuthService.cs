using KFS.Application.DTOs.Auth;

namespace KFS.Application.Services;

public interface IAuthService
{
    Task<AuthResponse> StudentLoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> AdminLoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
}
