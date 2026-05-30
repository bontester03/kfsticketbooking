using KFS.Application.DTOs.Passes;
using KFS.Domain.Enums;

namespace KFS.Application.Services;

public interface IAdminPassService
{
    Task<GeneratePassesResponse> GenerateBatchAsync(GeneratePassesRequest request, CancellationToken ct = default);
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
