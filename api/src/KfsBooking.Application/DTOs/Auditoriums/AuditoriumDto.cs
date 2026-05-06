namespace KfsBooking.Application.DTOs.Auditoriums;

public record AuditoriumDto(
    Guid Id,
    string Name,
    string Location,
    int Capacity,
    string? Description,
    bool IsActive);

public record CreateAuditoriumRequest(
    string Name,
    string Location,
    int Capacity,
    string? Description);

public record UpdateAuditoriumRequest(
    string Name,
    string Location,
    int Capacity,
    string? Description,
    bool IsActive);
