using KfsBooking.Domain.Enums;

namespace KfsBooking.Application.DTOs.Auth;

public record AuthResponse(
    string Token,
    DateTime ExpiresAt,
    Guid UserId,
    string Email,
    string FullName,
    UserRole Role);
