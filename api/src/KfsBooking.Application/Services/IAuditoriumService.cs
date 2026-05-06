using KfsBooking.Application.DTOs.Auditoriums;

namespace KfsBooking.Application.Services;

public interface IAuditoriumService
{
    Task<IReadOnlyList<AuditoriumDto>> GetAllAsync(CancellationToken ct = default);
    Task<AuditoriumDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AuditoriumDto> CreateAsync(CreateAuditoriumRequest request, CancellationToken ct = default);
    Task<AuditoriumDto> UpdateAsync(Guid id, UpdateAuditoriumRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
