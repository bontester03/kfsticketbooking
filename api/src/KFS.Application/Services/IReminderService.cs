using KFS.Application.DTOs.Reminders;

namespace KFS.Application.Services;

public interface IReminderService
{
    Task<int> SendUnbookedAsync(Guid eventId, SendUnbookedReminderRequest request, CancellationToken ct = default);
    /// <summary>Background job — iterates every event whose day-before reminder is due and sends it.</summary>
    Task<int> RunDayBeforeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReminderLogDto>> ListLogsAsync(Guid eventId, int take, CancellationToken ct = default);
}
