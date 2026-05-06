using System.Text;
using KFS.Application.Common;
using KFS.Application.Interfaces;
using KFS.Domain.Enums;

namespace KFS.Infrastructure.Email;

public class TicketEmailRenderer : ITicketEmailRenderer
{
    public string RenderTicket(TicketEmailModel m, byte[] qrPng)
    {
        var qrB64 = Convert.ToBase64String(qrPng);
        var arabicLine = $"المقاعد المحجوزة: {m.Row}{m.SeatNumber}";
        var category = m.Group == ZoneGroup.A ? "A" : "B";

        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <title>{{m.EventName}}</title>
        </head>
        <body style="font-family:Arial,Helvetica,sans-serif;background:#f5f7fb;padding:24px;color:#0f172a">
            <table align="center" width="560" style="background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:24px">
                <tr><td>
                    <h2 style="margin:0 0 8px 0;color:#1e3a8a">{{m.EventName}}</h2>
                    <p style="margin:0 0 16px 0;color:#475569">{{m.EventDate.FormatLocal()}} · {{m.Venue}}</p>
                    <table width="100%" cellpadding="6" style="background:#f8fafc;border-radius:8px;font-size:14px">
                        <tr><td style="color:#64748b;width:120px">#</td><td>****{{m.TicketLast6}}</td></tr>
                        <tr><td style="color:#64748b">CATEGORY</td><td>{{category}}</td></tr>
                        <tr><td style="color:#64748b">GATE</td><td>Gate {{(m.Group == ZoneGroup.A ? "A" : "B")}}</td></tr>
                        <tr><td style="color:#64748b">BLOCK</td><td>{{m.Block}}</td></tr>
                        <tr><td style="color:#64748b">SEAT</td><td>{{m.SeatNumber}}</td></tr>
                        <tr><td style="color:#64748b">ROW</td><td>{{m.Row}}</td></tr>
                        <tr><td style="color:#64748b">PARENT</td><td>{{m.ParentLabel}} of {{m.StudentName}}</td></tr>
                    </table>
                    <div dir="rtl" lang="ar" style="margin-top:14px;font-size:14px;color:#1e293b">{{arabicLine}}</div>
                    <div style="text-align:center;margin-top:16px">
                        <img alt="QR" src="data:image/png;base64,{{qrB64}}" style="width:220px;height:220px;border:1px solid #e2e8f0;border-radius:8px" />
                    </div>
                    {{(m.MapLink is null ? "" : $"<p style='margin-top:14px'><a href='{m.MapLink}' style='color:#1e40af'>Open venue map</a></p>")}}
                    <p style="margin-top:18px;color:#64748b;font-size:12px">Ticket sent to {{m.StudentEmail}} and pending approval by receiver.</p>
                </td></tr>
            </table>
        </body>
        </html>
        """;
    }

    public string RenderDayBefore(DayBeforeReminderModel m)
    {
        var sb = new StringBuilder();
        sb.Append($"""
        <!DOCTYPE html>
        <html><head><meta charset="utf-8"></head>
        <body style="font-family:Arial,Helvetica,sans-serif;background:#f5f7fb;padding:24px;color:#0f172a">
        <table align="center" width="560" style="background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:24px">
        <tr><td>
            <h2 style="margin:0 0 8px 0;color:#1e3a8a">Reminder — {m.EventName} tomorrow</h2>
            <p>Dear {m.StudentName},</p>
            <p>{m.EventName} is tomorrow ({m.EventDate.FormatLocal()}) at {m.Venue}.</p>
            <p><strong>Address:</strong> {m.VenueAddress}</p>
        """);
        if (!string.IsNullOrEmpty(m.MapLink))
            sb.Append($"<p><a href=\"{m.MapLink}\" style=\"color:#1e40af\">Open venue map</a></p>");
        if (!string.IsNullOrEmpty(m.AdminNote))
            sb.Append($"<p style='background:#fef3c7;padding:10px;border-radius:6px'>{m.AdminNote}</p>");

        sb.Append("<p>Please bring both QR tickets attached to this email:</p>");
        foreach (var (label, png) in m.Tickets)
        {
            var b64 = Convert.ToBase64String(png);
            sb.Append($"<div style='display:inline-block;margin:8px;text-align:center'><div>{label}</div><img alt='QR {label}' src='data:image/png;base64,{b64}' style='width:180px;height:180px;border:1px solid #e2e8f0;border-radius:6px' /></div>");
        }
        sb.Append("</td></tr></table></body></html>");
        return sb.ToString();
    }

    public string RenderUnbooked(UnbookedReminderModel m)
    {
        var custom = string.IsNullOrWhiteSpace(m.CustomBody)
            ? "<p>You have not booked your parents' seats yet for our upcoming event.</p>"
            : $"<p>{m.CustomBody}</p>";

        return $"""
        <!DOCTYPE html>
        <html><head><meta charset="utf-8"></head>
        <body style="font-family:Arial,Helvetica,sans-serif;background:#f5f7fb;padding:24px;color:#0f172a">
        <table align="center" width="560" style="background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:24px">
        <tr><td>
        <h2 style="margin:0 0 8px 0;color:#1e3a8a">{m.EventName} — please book your seats</h2>
        <p>Dear {m.StudentName},</p>
        {custom}
        <p>Event date: <strong>{m.EventDate.FormatLocal()}</strong>. Please log in to the portal to pick a row + seat for your mother and father.</p>
        </td></tr></table></body></html>
        """;
    }
}
