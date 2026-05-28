using System.IO.Compression;
using KFS.Application.Common.Exceptions;
using KFS.Application.DTOs.Passes;
using KFS.Application.Interfaces;
using KFS.Application.Services;
using KFS.Domain.Entities;
using KFS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KFS.Application.Services;

public class AdminPassService : IAdminPassService
{
    private readonly IApplicationDbContext _db;
    private readonly IQrCodeService _qr;
    private readonly IBlobStorage _blobs;
    private readonly IPassPdfRenderer _pdf;
    private readonly ICurrentUser _currentUser;

    public AdminPassService(IApplicationDbContext db, IQrCodeService qr, IBlobStorage blobs,
        IPassPdfRenderer pdf, ICurrentUser currentUser)
    {
        _db = db; _qr = qr; _blobs = blobs; _pdf = pdf; _currentUser = currentUser;
    }

    public async Task<GeneratePassesResponse> GenerateBatchAsync(GeneratePassesRequest request, CancellationToken ct = default)
    {
        if (request.Count <= 0 || request.Count > 1000)
            throw new AppException("bad_input", "Count must be between 1 and 1000.");

        var ev = await _db.Events.FirstOrDefaultAsync(e => e.IsActive, ct)
            ?? throw new AppException("no_active_event", "No active event.");

        var seatsPerCode = request.Type == AdminPassType.Guest ? 3 : 1;
        var requestedSeats = request.Count * seatsPerCode;

        // Enforce the per-type limit (the zone's capacity). Reject if this batch would exceed it.
        var zoneCode = ZoneCodeForType(request.Type);
        var zone = await _db.Zones.FirstOrDefaultAsync(z => z.EventId == ev.Id && z.Code == zoneCode, ct);
        var capacity = zone?.Capacity ?? 0;
        var alreadyIssued = await _db.AdminPasses
            .Where(p => p.EventId == ev.Id && p.Type == request.Type)
            .SumAsync(p => (int?)p.SeatsCount, ct) ?? 0;
        var remaining = capacity - alreadyIssued;
        if (requestedSeats > remaining)
            throw new AppException("quota_exceeded",
                $"{request.Type} limit is {capacity} seats. {alreadyIssued} already generated, {Math.Max(0, remaining)} remaining — cannot generate {requestedSeats} more.");

        var batchId = Guid.NewGuid();
        var qrExpiry = ev.EventDate.AddHours(36);

        var passes = new List<AdminPass>(request.Count);
        var pdfEntries = new List<PassPdfEntry>(request.Count);
        var pngs = new List<(string FileName, byte[] Bytes)>(request.Count);

        for (var i = 1; i <= request.Count; i++)
        {
            var ticket = $"KFS-{request.Type.ToString().ToUpper()}-{batchId.ToString("N")[..6].ToUpperInvariant()}-{i:D3}";
            var pass = new AdminPass
            {
                EventId = ev.Id,
                Type = request.Type,
                BatchId = batchId,
                SequenceNumber = i,
                TicketNumber = ticket,
                SeatsCount = seatsPerCode,
                IssuedByAdminId = _currentUser.UserId,
                IssuedAt = DateTime.UtcNow
            };
            pass.QrCodePayload = _qr.EncodePayload(new QrPayloadInput(
                pass.Id, ev.Id, ScannedItemType.AdminPass, ZoneCodeForType(request.Type),
                null, seatsPerCode, qrExpiry));

            var png = _qr.RenderPng(pass.QrCodePayload);
            pass.QrCodeImageUrl = await _blobs.SaveAsync($"qr-codes/{ev.Id}/{ticket}.png", png, "image/png", ct);

            passes.Add(pass);
            // Pool batches have no student linkage — Guest passes default to Gate A;
            // other types fall back to the theme's gate label.
            var gateForEntry = request.Type == AdminPassType.Guest ? "Gate A" : null;
            pdfEntries.Add(new PassPdfEntry(ticket, i, png, seatsPerCode, null, gateForEntry));
            pngs.Add(($"{ticket}.png", png));
        }

        _db.AdminPasses.AddRange(passes);
        await _db.SaveChangesAsync(ct);

        string downloadUrl;
        if (request.Format == PassOutputFormat.Pdf)
        {
            var pdfBytes = _pdf.RenderSheet(request.Type, ev.Name, ev.EventDate, pdfEntries);
            downloadUrl = await _blobs.SaveAsync(
                $"printable-batches/{ev.Id}/{batchId}.pdf", pdfBytes, "application/pdf", ct);
        }
        else
        {
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (name, bytes) in pngs)
                {
                    var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
                    using var es = entry.Open();
                    es.Write(bytes);
                }
            }
            downloadUrl = await _blobs.SaveAsync(
                $"printable-batches/{ev.Id}/{batchId}.zip", ms.ToArray(), "application/zip", ct);
        }

        return new GeneratePassesResponse(batchId, request.Count, downloadUrl, request.Format);
    }

    public async Task<IReadOnlyList<PassBatchSummaryDto>> ListBatchesAsync(CancellationToken ct = default)
    {
        var passes = await _db.AdminPasses
            .Select(p => new { p.Id, p.BatchId, p.Type, p.SeatsCount, p.IssuedAt })
            .ToListAsync(ct);

        // Passes with at least one valid scan (a "scanned" ticket).
        var scannedIds = (await _db.ScanLogs
            .Where(s => s.ScannedItemType == ScannedItemType.AdminPass && s.Result == ScanResult.Valid && s.ItemId != null)
            .Select(s => s.ItemId!.Value).Distinct().ToListAsync(ct)).ToHashSet();

        return passes
            .GroupBy(p => new { p.BatchId, p.Type })
            .Select(g => new PassBatchSummaryDto(
                g.Key.BatchId, g.Key.Type, g.Count(), g.Sum(x => x.SeatsCount), g.Min(x => x.IssuedAt),
                null, null, g.Count(x => scannedIds.Contains(x.Id))))
            .OrderByDescending(g => g.CreatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<AdminPassDto>> ListPassesAsync(Guid? batchId, CancellationToken ct = default)
    {
        var query = _db.AdminPasses.AsQueryable();
        if (batchId.HasValue) query = query.Where(p => p.BatchId == batchId.Value);
        var passes = await query.OrderBy(p => p.BatchId).ThenBy(p => p.SequenceNumber).Take(1000).ToListAsync(ct);

        // Valid-scan (admission) counts per pass, so the admin sees how many have entered.
        var passIds = passes.Select(p => p.Id).ToList();
        var admitted = await _db.ScanLogs
            .Where(s => s.ScannedItemType == ScannedItemType.AdminPass && s.Result == ScanResult.Valid
                        && s.ItemId != null && passIds.Contains(s.ItemId.Value))
            .GroupBy(s => s.ItemId!.Value)
            .Select(g => new { ItemId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var admittedByPass = admitted.ToDictionary(x => x.ItemId, x => x.Count);

        // For student-linked Guest passes, look up the child's VIP booking group to derive the gate.
        var studentIds = passes.Where(p => p.Type == AdminPassType.Guest && p.StudentId != null)
            .Select(p => p.StudentId!.Value).Distinct().ToList();
        var gateByStudent = studentIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _db.Bookings
                .Where(b => studentIds.Contains(b.StudentId) && b.Status == BookingStatus.Confirmed)
                .GroupBy(b => b.StudentId)
                .Select(g => new { StudentId = g.Key, Group = g.OrderByDescending(b => b.CreatedAt).First().GroupChosen })
                .ToListAsync(ct))
                .ToDictionary(x => x.StudentId, x => x.Group == ZoneGroup.B ? "Gate B" : "Gate A");

        return passes.Select(p => new AdminPassDto(
            p.Id, p.BatchId, p.Type, p.SequenceNumber, p.TicketNumber, p.SeatsCount,
            // Re-sign the SAS so on-screen previews don't 403 on an expired token.
            p.IssuedToName, p.QrCodeImageUrl is null ? null : _blobs.RefreshReadUrl(p.QrCodeImageUrl), p.IssuedAt,
            admittedByPass.TryGetValue(p.Id, out var c) ? c : 0,
            p.Type == AdminPassType.Guest && p.StudentId != null && gateByStudent.TryGetValue(p.StudentId.Value, out var g) ? g : null))
            .ToList();
    }

    public async Task<AdminPassDto> UpdateAsync(Guid passId, UpdatePassRequest request, CancellationToken ct = default)
    {
        var pass = await _db.AdminPasses.FindAsync(new object[] { passId }, ct)
            ?? throw new NotFoundException("AdminPass", passId);
        pass.IssuedToName = string.IsNullOrWhiteSpace(request.IssuedToName) ? null : request.IssuedToName.Trim();
        await _db.SaveChangesAsync(ct);
        return new AdminPassDto(pass.Id, pass.BatchId, pass.Type, pass.SequenceNumber, pass.TicketNumber,
            pass.SeatsCount, pass.IssuedToName,
            pass.QrCodeImageUrl is null ? null : _blobs.RefreshReadUrl(pass.QrCodeImageUrl), pass.IssuedAt);
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)> DownloadBatchAsync(
        Guid batchId, PassOutputFormat format, CancellationToken ct = default)
    {
        var passes = await _db.AdminPasses.Where(p => p.BatchId == batchId)
            .OrderBy(p => p.SequenceNumber).ToListAsync(ct);
        if (passes.Count == 0) throw new NotFoundException("Batch", batchId);

        var ev = await _db.Events.FindAsync(new object[] { passes[0].EventId }, ct)!
            ?? throw new NotFoundException("Event", passes[0].EventId);

        // For Guest passes linked to a child, look up their VIP booking to set the gate (A or B).
        // Other types and unlinked guest passes leave Gate null → renderer uses the theme default
        // (Gate G for unlinked guest in the renderer; here we default unlinked guest to Gate A).
        var studentIds = passes.Where(p => p.Type == AdminPassType.Guest && p.StudentId != null)
            .Select(p => p.StudentId!.Value).Distinct().ToList();
        var gateByStudent = studentIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _db.Bookings
                .Where(b => studentIds.Contains(b.StudentId) && b.Status == BookingStatus.Confirmed)
                .GroupBy(b => b.StudentId)
                .Select(g => new { StudentId = g.Key, Group = g.OrderByDescending(b => b.CreatedAt).First().GroupChosen })
                .ToListAsync(ct))
                .ToDictionary(x => x.StudentId, x => x.Group == ZoneGroup.B ? "Gate B" : "Gate A");

        var entries = passes.Select(p =>
        {
            string? gate = p.Type == AdminPassType.Guest
                ? (p.StudentId != null && gateByStudent.TryGetValue(p.StudentId.Value, out var g) ? g : "Gate A")
                : null;
            return new PassPdfEntry(p.TicketNumber, p.SequenceNumber, _qr.RenderPng(p.QrCodePayload),
                p.SeatsCount, p.IssuedToName, gate);
        }).ToList();

        if (format == PassOutputFormat.Pdf)
        {
            var bytes = _pdf.RenderSheet(passes[0].Type, ev.Name, ev.EventDate, entries);
            return (bytes, "application/pdf", $"{passes[0].Type}-{batchId}.pdf");
        }

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var p in passes)
            {
                var png = _qr.RenderPng(p.QrCodePayload);
                var entry = zip.CreateEntry($"{p.TicketNumber}.png", CompressionLevel.Fastest);
                using var es = entry.Open();
                es.Write(png);
            }
        }
        return (ms.ToArray(), "application/zip", $"{passes[0].Type}-{batchId}.zip");
    }

    public async Task<int> DeleteAllAsync(AdminPassType? type, CancellationToken ct = default)
    {
        IQueryable<Domain.Entities.AdminPass> q = _db.AdminPasses;
        if (type.HasValue) q = q.Where(p => p.Type == type.Value);
        var passes = await q.ToListAsync(ct);
        if (passes.Count == 0) return 0;

        var ids = passes.Select(p => p.Id).ToList();
        var scans = await _db.ScanLogs
            .Where(s => s.ScannedItemType == ScannedItemType.AdminPass && s.ItemId != null && ids.Contains(s.ItemId.Value))
            .ToListAsync(ct);
        if (scans.Count > 0) _db.ScanLogs.RemoveRange(scans);

        _db.AdminPasses.RemoveRange(passes);
        await _db.SaveChangesAsync(ct);
        return passes.Count;
    }

    public async Task<int> DeleteBatchAsync(Guid batchId, CancellationToken ct = default)
    {
        var passes = await _db.AdminPasses.Where(p => p.BatchId == batchId).ToListAsync(ct);
        if (passes.Count == 0) throw new NotFoundException("Batch", batchId);

        // Remove their scan history first so the audit doesn't reference deleted tickets.
        var ids = passes.Select(p => p.Id).ToList();
        var scans = await _db.ScanLogs
            .Where(s => s.ScannedItemType == ScannedItemType.AdminPass && s.ItemId != null && ids.Contains(s.ItemId.Value))
            .ToListAsync(ct);
        if (scans.Count > 0) _db.ScanLogs.RemoveRange(scans);

        _db.AdminPasses.RemoveRange(passes);
        await _db.SaveChangesAsync(ct);
        return passes.Count;
    }

    public async Task<IReadOnlyList<PassQuotaDto>> GetQuotasAsync(CancellationToken ct = default)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.IsActive, ct)
            ?? throw new AppException("no_active_event", "No active event.");

        var zones = await _db.Zones.Where(z => z.EventId == ev.Id).ToListAsync(ct);
        var issuedByType = await _db.AdminPasses
            .Where(p => p.EventId == ev.Id)
            .GroupBy(p => p.Type)
            .Select(g => new { Type = g.Key, Seats = g.Sum(x => x.SeatsCount) })
            .ToListAsync(ct);

        var types = new[] { AdminPassType.VVIP, AdminPassType.Guest, AdminPassType.Staff, AdminPassType.Media };
        return types.Select(t =>
        {
            var code = ZoneCodeForType(t);
            var capacity = zones.FirstOrDefault(z => z.Code == code)?.Capacity ?? 0;
            var issued = issuedByType.FirstOrDefault(x => x.Type == t)?.Seats ?? 0;
            return new PassQuotaDto(t, t.ToString(), capacity, issued, Math.Max(0, capacity - issued));
        }).ToList();
    }

    public async Task<PassQuotaDto> SetQuotaAsync(SetPassQuotaRequest request, CancellationToken ct = default)
    {
        if (request.Capacity < 0) throw new AppException("bad_input", "Limit cannot be negative.");

        var ev = await _db.Events.FirstOrDefaultAsync(e => e.IsActive, ct)
            ?? throw new AppException("no_active_event", "No active event.");

        var code = ZoneCodeForType(request.Type);
        var zone = await _db.Zones.FirstOrDefaultAsync(z => z.EventId == ev.Id && z.Code == code, ct)
            ?? throw new NotFoundException("Zone", code);

        var issued = await _db.AdminPasses
            .Where(p => p.EventId == ev.Id && p.Type == request.Type)
            .SumAsync(p => (int?)p.SeatsCount, ct) ?? 0;
        if (request.Capacity < issued)
            throw new AppException("bad_input",
                $"Cannot set the limit below the {issued} seats already generated for {request.Type}.");

        zone.Capacity = request.Capacity;
        await _db.SaveChangesAsync(ct);
        return new PassQuotaDto(request.Type, request.Type.ToString(), zone.Capacity, issued, Math.Max(0, zone.Capacity - issued));
    }

    private static ZoneCode ZoneCodeForType(AdminPassType type) => type switch
    {
        AdminPassType.VVIP  => ZoneCode.VVIP,
        AdminPassType.Guest => ZoneCode.GUEST,
        AdminPassType.Staff => ZoneCode.STAFF,
        AdminPassType.Media => ZoneCode.MEDIA,
        _ => throw new AppException("bad_input", "Unsupported pass type.")
    };
}
