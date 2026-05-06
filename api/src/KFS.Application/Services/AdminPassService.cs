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
            pass.QrCodeImageUrl = await _blobs.SaveAsync($"qr/{ev.Id}/{ticket}.png", png, "image/png", ct);

            passes.Add(pass);
            pdfEntries.Add(new PassPdfEntry(ticket, i, png, seatsPerCode, null));
            pngs.Add(($"{ticket}.png", png));
        }

        _db.AdminPasses.AddRange(passes);
        await _db.SaveChangesAsync(ct);

        string downloadUrl;
        if (request.Format == PassOutputFormat.Pdf)
        {
            var pdfBytes = _pdf.RenderSheet(request.Type, ev.Name, ev.EventDate, pdfEntries);
            downloadUrl = await _blobs.SaveAsync(
                $"batches/{ev.Id}/{batchId}.pdf", pdfBytes, "application/pdf", ct);
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
                $"batches/{ev.Id}/{batchId}.zip", ms.ToArray(), "application/zip", ct);
        }

        return new GeneratePassesResponse(batchId, request.Count, downloadUrl, request.Format);
    }

    public async Task<IReadOnlyList<PassBatchSummaryDto>> ListBatchesAsync(CancellationToken ct = default)
    {
        var grouped = await _db.AdminPasses
            .GroupBy(p => new { p.BatchId, p.Type })
            .Select(g => new
            {
                g.Key.BatchId,
                g.Key.Type,
                Count = g.Count(),
                SeatsTotal = g.Sum(x => x.SeatsCount),
                CreatedAt = g.Min(x => x.IssuedAt)
            })
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(ct);

        return grouped.Select(g => new PassBatchSummaryDto(
            g.BatchId, g.Type, g.Count, g.SeatsTotal, g.CreatedAt, null, null)).ToList();
    }

    public async Task<IReadOnlyList<AdminPassDto>> ListPassesAsync(Guid? batchId, CancellationToken ct = default)
    {
        var query = _db.AdminPasses.AsQueryable();
        if (batchId.HasValue) query = query.Where(p => p.BatchId == batchId.Value);
        var passes = await query.OrderBy(p => p.BatchId).ThenBy(p => p.SequenceNumber).Take(1000).ToListAsync(ct);
        return passes.Select(p => new AdminPassDto(
            p.Id, p.BatchId, p.Type, p.SequenceNumber, p.TicketNumber, p.SeatsCount,
            p.IssuedToName, p.QrCodeImageUrl, p.IssuedAt)).ToList();
    }

    public async Task<AdminPassDto> UpdateAsync(Guid passId, UpdatePassRequest request, CancellationToken ct = default)
    {
        var pass = await _db.AdminPasses.FindAsync(new object[] { passId }, ct)
            ?? throw new NotFoundException("AdminPass", passId);
        pass.IssuedToName = string.IsNullOrWhiteSpace(request.IssuedToName) ? null : request.IssuedToName.Trim();
        await _db.SaveChangesAsync(ct);
        return new AdminPassDto(pass.Id, pass.BatchId, pass.Type, pass.SequenceNumber, pass.TicketNumber,
            pass.SeatsCount, pass.IssuedToName, pass.QrCodeImageUrl, pass.IssuedAt);
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)> DownloadBatchAsync(
        Guid batchId, PassOutputFormat format, CancellationToken ct = default)
    {
        var passes = await _db.AdminPasses.Where(p => p.BatchId == batchId)
            .OrderBy(p => p.SequenceNumber).ToListAsync(ct);
        if (passes.Count == 0) throw new NotFoundException("Batch", batchId);

        var ev = await _db.Events.FindAsync(new object[] { passes[0].EventId }, ct)!
            ?? throw new NotFoundException("Event", passes[0].EventId);

        var entries = passes.Select(p => new PassPdfEntry(
            p.TicketNumber, p.SequenceNumber, _qr.RenderPng(p.QrCodePayload),
            p.SeatsCount, p.IssuedToName)).ToList();

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

    private static ZoneCode ZoneCodeForType(AdminPassType type) => type switch
    {
        AdminPassType.VVIP  => ZoneCode.VVIP,
        AdminPassType.Guest => ZoneCode.GUEST,
        AdminPassType.Staff => ZoneCode.STAFF,
        AdminPassType.Media => ZoneCode.MEDIA,
        _ => throw new AppException("bad_input", "Unsupported pass type.")
    };
}
