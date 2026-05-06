namespace KFS.Application.DTOs.Reminders;

public record SendUnbookedReminderRequest(string? CustomBody, string? CustomSubject);

public record ReminderLogDto(
    Guid Id,
    string Type,
    Guid? StudentId,
    string? StudentEmail,
    DateTime SentAt,
    string? EmailMessageId);
