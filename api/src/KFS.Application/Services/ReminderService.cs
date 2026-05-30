using KFS.Application.Common.Exceptions;
using KFS.Application.DTOs.Reminders;
using KFS.Application.Interfaces;
using KFS.Domain.Entities;
using KFS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KFS.Application.Services;

public class ReminderService : IReminderService
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailService _email;
    private readonly ITicketEmailRenderer _renderer;
    private readonly IQrCodeService _qr;
    private readonly ILogger<ReminderService> _log;

    public ReminderService(IApplicationDbContext db, IEmailService email, ITicketEmailRenderer renderer,
        IQrCodeService qr, ILogger<ReminderService> log)
    {
        _db = db; _email = email; _renderer = renderer; _qr = qr; _log = log;
    }

    public async Task<int> SendUnbookedAsync(Guid eventId, SendUnbookedReminderRequest request, CancellationToken ct = default)
    {
        var ev = await _db.Events.FindAsync(new object[] { eventId }, ct)
            ?? throw new NotFoundException("Event", eventId);

        // Only THIS event's students who haven't confirmed for THIS event.
        var unbooked = await _db.Students
            .Where(s => s.IsActive && s.EventId == ev.Id
                        && !_db.Bookings.Any(b => b.StudentId == s.Id && b.EventId == ev.Id
                                                  && b.Status == BookingStatus.Confirmed))
            .ToListAsync(ct);

        var sent = 0;
        foreach (var s in unbooked)
        {
            var html = _renderer.RenderUnbooked(new UnbookedReminderModel(
                s.Email, $"{s.FirstName} {s.LastName}", ev.Name, ev.EventDate, request.CustomBody));
            try
            {
                var msgId = await _email.SendAsync(new OutgoingEmail(
                    s.Email, request.CustomSubject ?? $"{ev.Name} — please book your seats", html), ct);
                _db.ReminderLogs.Add(new ReminderLog
                {
                    EventId = ev.Id, StudentId = s.Id, Type = ReminderType.Unbooked, EmailMessageId = msgId
                });
                sent++;
            }
            catch (Exception ex) { _log.LogError(ex, "Failed unbooked reminder to {Email}", s.Email); }
        }
        await _db.SaveChangesAsync(ct);
        return sent;
    }

    public async Task<int> RunDayBeforeAsync(CancellationToken ct = default)
    {
        // Iterate every active event that hasn't sent its day-before yet.
        var due = await _db.Events
            .Where(e => e.IsActive && !e.ReminderDayBeforeSent)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var totalSent = 0;
        foreach (var ev in due)
        {
            var hoursAway = (ev.EventDate - now).TotalHours;
            if (hoursAway > 24 || hoursAway < 0) continue;

            var bookings = await _db.Bookings
                .Where(b => b.EventId == ev.Id && b.Status == BookingStatus.Confirmed)
                .Include(b => b.Student)
                .Include(b => b.Items)
                .ToListAsync(ct);

            var sent = 0;
            foreach (var b in bookings)
            {
                if (b.Student is null) continue;
                var tickets = b.Items.Select(i => ($"{i.ParentRole}", _qr.RenderPng(i.QrCodePayload!))).ToList();
                var html = _renderer.RenderDayBefore(new DayBeforeReminderModel(
                    b.Student.Email, $"{b.Student.FirstName} {b.Student.LastName}",
                    ev.Name, ev.EventDate, ev.Venue, ev.VenueAddress, ev.MapLink, ev.ReminderNoteFromAdmin,
                    tickets));
                try
                {
                    var msgId = await _email.SendAsync(new OutgoingEmail(
                        b.Student.Email, $"Reminder: {ev.Name} tomorrow", html,
                        tickets.Select(t => new EmailAttachment($"{t.Item1}.png", "image/png", t.Item2)).ToList()), ct);
                    _db.ReminderLogs.Add(new ReminderLog
                    {
                        EventId = ev.Id, StudentId = b.Student.Id,
                        Type = ReminderType.DayBefore, EmailMessageId = msgId
                    });
                    sent++;
                }
                catch (Exception ex) { _log.LogError(ex, "Failed day-before reminder to {Email}", b.Student.Email); }
            }

            ev.ReminderDayBeforeSent = sent > 0;
            totalSent += sent;
        }
        await _db.SaveChangesAsync(ct);
        return totalSent;
    }

    public async Task<IReadOnlyList<ReminderLogDto>> ListLogsAsync(Guid eventId, int take, CancellationToken ct = default)
    {
        var rows = await _db.ReminderLogs
            .Where(r => r.EventId == eventId)
            .OrderByDescending(r => r.SentAt)
            .Take(Math.Clamp(take, 1, 500))
            .Include(r => r.Student)
            .ToListAsync(ct);
        return rows.Select(r => new ReminderLogDto(
            r.Id, r.Type.ToString(), r.StudentId,
            r.Student?.Email, r.SentAt, r.EmailMessageId)).ToList();
    }
}
