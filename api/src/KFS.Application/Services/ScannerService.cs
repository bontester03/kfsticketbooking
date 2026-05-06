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
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.IsActive, ct);
        if (ev is null || string.IsNullOrEmpty(ev.ScannerToken) || ev.ScannerToken != request.EventToken)
            return Fail(ScanResult.Invalid, "Scanner token invalid.");

        QrPayloadDecoded payload;
        try { payload = _qr.DecodePayload(request.QrPayload); }
        catch (Exception ex)
        {
            _log.LogInformation(ex, "Invalid QR payload presented");
            await LogAsync(ScannedItemType.BookingItem, null, ScanResult.Invalid, scannerIp, request.DeviceInfo, ct);
            return Fail(ScanResult.Invalid, "QR is not valid.");
        }

        if (payload.ExpiresAt < DateTime.UtcNow)
        {
            await LogAsync(payload.ItemType, payload.TicketId, ScanResult.Expired, scannerIp, request.DeviceInfo, ct);
            return Fail(ScanResult.Expired, "QR has expired.");
        }

        if (payload.ItemType == ScannedItemType.BookingItem)
            return await VerifyBookingItemAsync(payload, scannerIp, request.DeviceInfo, ct);
        return await VerifyAdminPassAsync(payload, scannerIp, request.DeviceInfo, ct);
    }

    private async Task<ScanResponse> VerifyBookingItemAsync(QrPayloadDecoded payload, string? ip, string? device, CancellationToken ct)
    {
        var item = await _db.BookingItems
            .Include(bi => bi.Booking).ThenInclude(b => b!.Student)
            .Include(bi => bi.Zone)
            .Include(bi => bi.Seat)
            .FirstOrDefaultAsync(bi => bi.Id == payload.TicketId, ct);

        if (item is null || item.Booking!.Status != BookingStatus.Confirmed)
        {
            await LogAsync(ScannedItemType.BookingItem, payload.TicketId, ScanResult.Invalid, ip, device, ct);
            return Fail(ScanResult.Invalid, "Ticket not found or not confirmed.");
        }

        var prior = await _db.ScanLogs.FirstOrDefaultAsync(s =>
            s.ItemId == item.Id && s.ScannedItemType == ScannedItemType.BookingItem && s.Result == ScanResult.Valid, ct);
        if (prior != null)
        {
            await LogAsync(ScannedItemType.BookingItem, item.Id, ScanResult.AlreadyUsed, ip, device, ct);
            return new ScanResponse(false, ScanResult.AlreadyUsed, ScannedItemType.BookingItem,
                item.Zone!.DisplayName, item.Seat!.FullLabel, 1,
                $"{item.Booking.Student?.FirstName} {item.Booking.Student?.LastName}".Trim(),
                true, prior.ScannedAt, $"Already scanned at {prior.ScannedAt:HH:mm}");
        }

        await LogAsync(ScannedItemType.BookingItem, item.Id, ScanResult.Valid, ip, device, ct);
        var holder = item.Booking.Student is null ? null
            : $"{item.ParentRole} of {item.Booking.Student.FirstName} {item.Booking.Student.LastName}";

        return new ScanResponse(true, ScanResult.Valid, ScannedItemType.BookingItem,
            item.Zone!.DisplayName, item.Seat!.FullLabel, 1, holder, false, null,
            $"Welcome — {item.Zone.DisplayName}, Seat {item.Seat.FullLabel}");
    }

    private async Task<ScanResponse> VerifyAdminPassAsync(QrPayloadDecoded payload, string? ip, string? device, CancellationToken ct)
    {
        var pass = await _db.AdminPasses.FirstOrDefaultAsync(p => p.Id == payload.TicketId, ct);
        if (pass is null)
        {
            await LogAsync(ScannedItemType.AdminPass, payload.TicketId, ScanResult.Invalid, ip, device, ct);
            return Fail(ScanResult.Invalid, "Pass not found.");
        }

        var prior = await _db.ScanLogs.FirstOrDefaultAsync(s =>
            s.ItemId == pass.Id && s.ScannedItemType == ScannedItemType.AdminPass && s.Result == ScanResult.Valid, ct);
        if (prior != null)
        {
            await LogAsync(ScannedItemType.AdminPass, pass.Id, ScanResult.AlreadyUsed, ip, device, ct);
            return new ScanResponse(false, ScanResult.AlreadyUsed, ScannedItemType.AdminPass,
                pass.Type.ToString(), null, pass.SeatsCount, pass.IssuedToName,
                true, prior.ScannedAt, $"Already scanned at {prior.ScannedAt:HH:mm}");
        }

        await LogAsync(ScannedItemType.AdminPass, pass.Id, ScanResult.Valid, ip, device, ct);
        var msg = pass.SeatsCount > 1
            ? $"Welcome — {pass.Type}. Group of {pass.SeatsCount} — please admit {pass.SeatsCount} people."
            : $"Welcome — {pass.Type}.";
        return new ScanResponse(true, ScanResult.Valid, ScannedItemType.AdminPass, pass.Type.ToString(),
            null, pass.SeatsCount, pass.IssuedToName, false, null, msg);
    }

    private async Task LogAsync(ScannedItemType type, Guid? id, ScanResult result, string? ip, string? device, CancellationToken ct)
    {
        _db.ScanLogs.Add(new ScanLog
        {
            ScannedItemType = type, ItemId = id, Result = result, ScannerIp = ip, DeviceInfo = device
        });
        await _db.SaveChangesAsync(ct);
    }

    private static ScanResponse Fail(ScanResult result, string message) =>
        new(false, result, null, null, null, null, null, false, null, message);
}
