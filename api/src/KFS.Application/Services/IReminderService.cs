using KFS.Application.DTOs.Reminders;

namespace KFS.Application.Services;

public interface IReminderService
{
    Task<int> SendUnbookedAsync(SendUnbookedReminderRequest request, CancellationToken ct = default);
    Task<int> RunDayBeforeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReminderLogDto>> ListLogsAsync(int take, CancellationToken ct = default);
}
