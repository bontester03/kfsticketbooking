using KFS.Application.DTOs.Passes;
using KFS.Domain.Enums;

namespace KFS.Application.Services;

public interface IAdminPassService
{
    Task<GeneratePassesResponse> GenerateBatchAsync(GeneratePassesRequest request, CancellationToken ct = default);

    // ----- Roster: 3-step UX (Upload preview → Generate QRs → Send emails) -----

    /// <summary>Step 1 — dry-run preview. Parses the XLSX and reports what would
    /// happen if the admin confirmed (rows to import, duplicates, quota state).
    /// No DB changes.</summary>
    Task<RosterPreviewDto> PreviewRosterAsync(
        Guid eventId, KFS.Domain.Enums.AdminPassType type, Stream xlsxStream, CancellationToken ct = default);

    /// <summary>Step 2 — generate one AdminPass + QR per non-duplicate row.
    /// Does NOT send emails; the admin triggers that explicitly in Step 3.</summary>
    Task<GenerateFromRosterResponse> GenerateFromRosterAsync(
        Guid eventId, KFS.Domain.Enums.AdminPassType type, Stream xlsxStream, CancellationToken ct = default);

    /// <summary>Step 3a — send the "your pass" email for every roster-generated
    /// pass in the batch that hasn't been emailed yet (or all if force=true).</summary>
    Task<SendBatchEmailsResponse> SendBatchEmailsAsync(Guid batchId, bool force, CancellationToken ct = default);

    /// <summary>Step 3b — resend the email for a single pass, regardless of EmailSent state.</summary>
    Task<AdminPassDto> ResendPassEmailAsync(Guid passId, CancellationToken ct = default);
    /// <summary>Scoped to one event — the admin event-picker UI passes its eventId.</summary>
    Task<IReadOnlyList<PassBatchSummaryDto>> ListBatchesAsync(Guid eventId, CancellationToken ct = default);
    Task<IReadOnlyList<AdminPassDto>> ListPassesAsync(Guid eventId, Guid? batchId, CancellationToken ct = default);
    Task<AdminPassDto> UpdateAsync(Guid passId, UpdatePassRequest request, CancellationToken ct = default);
    Task<(byte[] Bytes, string ContentType, string FileName)> DownloadBatchAsync(Guid batchId, PassOutputFormat format, CancellationToken ct = default);
    Task<int> DeleteBatchAsync(Guid batchId, CancellationToken ct = default);
    /// <summary>Deletes every pass for one event (optionally filtered by type) and their scan history. Frees the quota.</summary>
    Task<int> DeleteAllAsync(Guid eventId, KFS.Domain.Enums.AdminPassType? type, CancellationToken ct = default);
    Task<IReadOnlyList<PassQuotaDto>> GetQuotasAsync(Guid eventId, CancellationToken ct = default);
    Task<PassQuotaDto> SetQuotaAsync(SetPassQuotaRequest request, CancellationToken ct = default);
}
