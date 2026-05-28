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
    bool MustChangePassword,
    /// <summary>For students: the VIP group the school assigned them in the roster
    /// (1 = VIP A, 2 = VIP B). Null when not yet assigned or for admins.</summary>
    int? AssignedGroup = null);
