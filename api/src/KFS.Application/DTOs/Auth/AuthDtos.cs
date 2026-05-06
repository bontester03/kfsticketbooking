using KFS.Domain.Enums;

namespace KFS.Application.DTOs.Auth;

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Token, string NewPassword);

public record AuthResponse(
    string AccessToken,
    DateTime AccessExpiresAt,
    string RefreshToken,
    DateTime RefreshExpiresAt,
    Guid UserId,
    UserType UserType,
    string Email,
    string DisplayName,
    bool MustChangePassword);
