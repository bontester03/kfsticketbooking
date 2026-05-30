using KFS.Application.DTOs.Scan;
using KFS.Application.Interfaces;
using KFS.Domain.Entities;
using KFS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KFS.Application.Services;

public class ScannerService : IScannerService
{
    private readonly IApplicationDbContext _db;
    private readonly IQrCodeService _qr;
    private readonly ILogger<ScannerService> _log;

    public ScannerService(IApplicationDbContext db, IQrCodeService qr, ILogger<ScannerService> log)
    {
        _db = db; _qr = qr; _log = log;
    }

    public async Task<ScanResponse> VerifyAsync(ScanRequest request, string? scannerIp, CancellationToken ct = default)
    {
        // Each event (Boys/Girls) has its own scanner token — the URL on the iPad
        // identifies which gate this scanner is manning.
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.ScannerToken == request.EventToken, ct);
        if (ev is null) return Fail(ScanResult.Invalid, "Scanner token invalid.");

        QrPayloadDecoded payload;
        try { payload = _qr.DecodePayload(request.QrPayload); }
        catch (Exception ex)
        {
            _log.LogInformation(ex, "Invalid QR payload presented");
            await LogAsync(ev.Id, ScannedItemType.BookingItem, null, ScanResult.Invalid, scannerIp, request.DeviceInfo, ct);
            return Fail(ScanResult.Invalid, "QR is not valid.");
        }

        if (payload.ExpiresAt < DateTime.UtcNow)
        {
            await LogAsync(ev.Id, payload.ItemType, payload.TicketId, ScanResult.Expired, scannerIp, request.DeviceInfo, ct);
            return Fail(ScanResult.Expired, "QR has expired.");
        }

        if (payload.ItemType == ScannedItemType.BookingItem)
            return await VerifyBookingItemAsync(ev.Id, payload, scannerIp, request.DeviceInfo, ct);
        return await VerifyAdminPassAsync(ev.Id, payload, scannerIp, request.DeviceInfo, ct);
    }

    private async Task<ScanResponse> VerifyBookingItemAsync(Guid eventId, QrPayloadDecoded payload, string? ip, string? device, CancellationToken ct)
    {
        var item = await _db.BookingItems
            .Include(bi => bi.Booking).ThenInclude(b => b!.Student)
            .Include(bi => bi.Zone)
            .Include(bi => bi.Seat)
            .FirstOrDefaultAsync(bi => bi.Id == payload.TicketId, ct);

        if (item is null || item.Booking!.Status != BookingStatus.Confirmed)
        {
            await LogAsync(eventId, ScannedItemType.BookingItem, payload.TicketId, ScanResult.Invalid, ip, device, ct);
            return Fail(ScanResult.Invalid, "Ticket not found or not confirmed.");
        }

        // Cross-event check: a ticket from the Boys event scanned at the Girls gate
        // is invalid even though the QR signature is valid.
        if (item.Booking.EventId != eventId)
        {
            await LogAsync(eventId, ScannedItemType.BookingItem, item.Id, ScanResult.Invalid, ip, device, ct);
            return Fail(ScanResult.Invalid, "Ticket is for a different event.");
        }

        // A seat ticket admits exactly one person.
        var firstPrior = await _db.ScanLogs
            .Where(s => s.ItemId == item.Id && s.ScannedItemType == ScannedItemType.BookingItem && s.Result == ScanResult.Valid)
            .OrderBy(s => s.ScannedAt).FirstOrDefaultAsync(ct);
        var holder = item.Booking.Student is null ? null
            : $"{item.ParentRole} of {item.Booking.Student.FirstName} {item.Booking.Student.LastName}";

        if (firstPrior != null)
        {
            await LogAsync(eventId, ScannedItemType.BookingItem, item.Id, ScanResult.AlreadyUsed, ip, device, ct);
            return new ScanResponse(false, ScanResult.AlreadyUsed, ScannedItemType.BookingItem,
                item.Zone!.DisplayName, item.Seat!.FullLabel, 1, 1, holder,
                true, firstPrior.ScannedAt, $"Already scanned at {firstPrior.ScannedAt:HH:mm}.");
        }

        await LogAsync(eventId, ScannedItemType.BookingItem, item.Id, ScanResult.Valid, ip, device, ct);
        return new ScanResponse(true, ScanResult.Valid, ScannedItemType.BookingItem,
            item.Zone!.DisplayName, item.Seat!.FullLabel, 1, 1, holder, false, null,
            $"Welcome — {item.Zone.DisplayName}, Seat {item.Seat.FullLabel}.");
    }

    private async Task<ScanResponse> VerifyAdminPassAsync(Guid eventId, QrPayloadDecoded payload, string? ip, string? device, CancellationToken ct)
    {
        var pass = await _db.AdminPasses.FirstOrDefaultAsync(p => p.Id == payload.TicketId, ct);
        if (pass is null)
        {
            await LogAsync(eventId, ScannedItemType.AdminPass, payload.TicketId, ScanResult.Invalid, ip, device, ct);
            return Fail(ScanResult.Invalid, "Pass not found.");
        }

        // Cross-event check: a Boys-event pass can't admit at the Girls gate.
        if (pass.EventId != eventId)
        {
            await LogAsync(eventId, ScannedItemType.AdminPass, pass.Id, ScanResult.Invalid, ip, device, ct);
            return Fail(ScanResult.Invalid, "Pass is for a different event.");
        }

        // A pass admits up to SeatsCount people (Guest = 3, or 5 for the Girls event).
        var allowed = Math.Max(1, pass.SeatsCount);
        var priorScans = await _db.ScanLogs
            .Where(s => s.ItemId == pass.Id && s.ScannedItemType == ScannedItemType.AdminPass && s.Result == ScanResult.Valid)
            .OrderBy(s => s.ScannedAt).ToListAsync(ct);
        var used = priorScans.Count;

        if (used >= allowed)
        {
            await LogAsync(eventId, ScannedItemType.AdminPass, pass.Id, ScanResult.AlreadyUsed, ip, device, ct);
            var first = priorScans.First().ScannedAt;
            return new ScanResponse(false, ScanResult.AlreadyUsed, ScannedItemType.AdminPass,
                pass.Type.ToString(), null, allowed, used, pass.IssuedToName, true, first,
                allowed > 1
                    ? $"All {allowed} admissions already used (first at {first:HH:mm})."
                    : $"Already scanned at {first:HH:mm}.");
        }

        await LogAsync(eventId, ScannedItemType.AdminPass, pass.Id, ScanResult.Valid, ip, device, ct);
        var admitted = used + 1;
        var remaining = allowed - admitted;
        var msg = allowed > 1
            ? (remaining > 0
                ? $"Admit 1 — {pass.Type}. Person {admitted} of {allowed}; {remaining} entr{(remaining == 1 ? "y" : "ies")} left."
                : $"Admit 1 — {pass.Type}. Person {admitted} of {allowed} — final entry.")
            : $"Welcome — {pass.Type}.";
        return new ScanResponse(true, ScanResult.Valid, ScannedItemType.AdminPass, pass.Type.ToString(),
            null, allowed, admitted, pass.IssuedToName, false, null, msg);
    }

    private async Task LogAsync(Guid eventId, ScannedItemType type, Guid? id, ScanResult result, string? ip, string? device, CancellationToken ct)
    {
        _db.ScanLogs.Add(new ScanLog
        {
            EventId = eventId,
            ScannedItemType = type, ItemId = id, Result = result, ScannerIp = ip, DeviceInfo = device
        });
        await _db.SaveChangesAsync(ct);
    }

    private static ScanResponse Fail(ScanResult result, string message) =>
        new(false, result, null, null, null, null, 0, null, false, null, message);
}
