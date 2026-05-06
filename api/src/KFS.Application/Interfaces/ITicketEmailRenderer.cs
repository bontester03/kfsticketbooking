using KFS.Domain.Enums;

namespace KFS.Application.Interfaces;

public record TicketEmailModel(
    string StudentEmail,
    string ParentLabel,
    string StudentName,
    string TicketLast6,
    ZoneGroup Group,
    string Block,
    string Row,
    int SeatNumber,
    string EventName,
    DateTime EventDate,
    string Venue,
    string? MapLink);

public record DayBeforeReminderModel(
    string StudentEmail,
    string StudentName,
    string EventName,
    DateTime EventDate,
    string Venue,
    string VenueAddress,
    string? MapLink,
    string? AdminNote,
    IReadOnlyList<(string Label, byte[] QrPng)> Tickets);

public record UnbookedReminderModel(
    string StudentEmail,
    string StudentName,
    string EventName,
    DateTime EventDate,
    string? CustomBody);

public interface ITicketEmailRenderer
{
    string RenderTicket(TicketEmailModel model, byte[] qrPng);
    string RenderDayBefore(DayBeforeReminderModel model);
    string RenderUnbooked(UnbookedReminderModel model);
}
