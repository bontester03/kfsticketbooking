using KfsBooking.Domain.Enums;

namespace KfsBooking.Application.DTOs.Auth;

public record RegisterRequest(string FullName, string Email, string Password, UserRole Role = UserRole.Student);
