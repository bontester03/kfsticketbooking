using KFS.Application.DTOs.Events;

namespace KFS.Application.Services;

public interface IEventService
{
    // ---------- Admin ----------
    Task<IReadOnlyList<EventDto>> ListAsync(CancellationToken ct = default);
    Task<EventDto> GetByIdAsync(Guid eventId, CancellationToken ct = default);
    Task<EventDto> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<EventDto> UpdateAsync(Guid eventId, UpdateEventRequest request, CancellationToken ct = default);

    // ---------- Student (uses ICurrentUser to resolve EventId) ----------
    Task<EventDto> GetForCurrentStudentAsync(CancellationToken ct = default);

    // ---------- Public ----------
    /// <summary>One summary per event (Boys + Girls). The pre-auth landing page
    /// uses both so the visitor picks which session they want to attend.</summary>
    Task<IReadOnlyList<PublicEventDto>> ListPublicSummariesAsync(CancellationToken ct = default);
}
