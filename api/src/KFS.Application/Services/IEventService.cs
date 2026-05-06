using KFS.Application.DTOs.Events;

namespace KFS.Application.Services;

public interface IEventService
{
    Task<EventDto> GetActiveAsync(CancellationToken ct = default);
    Task<EventDto> UpdateAsync(Guid eventId, UpdateEventRequest request, CancellationToken ct = default);
}
