using KFS.Application.DTOs.Passes;
using KFS.Domain.Enums;

namespace KFS.Application.Services;

public interface IAdminPassService
{
    Task<GeneratePassesResponse> GenerateBatchAsync(GeneratePassesRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<PassBatchSummaryDto>> ListBatchesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdminPassDto>> ListPassesAsync(Guid? batchId, CancellationToken ct = default);
    Task<AdminPassDto> UpdateAsync(Guid passId, UpdatePassRequest request, CancellationToken ct = default);
    Task<(byte[] Bytes, string ContentType, string FileName)> DownloadBatchAsync(Guid batchId, PassOutputFormat format, CancellationToken ct = default);
    Task<int> DeleteBatchAsync(Guid batchId, CancellationToken ct = default);
    /// <summary>Deletes every pass (optionally filtered by type) and their scan history. Frees the quota.</summary>
    Task<int> DeleteAllAsync(KFS.Domain.Enums.AdminPassType? type, CancellationToken ct = default);
    Task<IReadOnlyList<PassQuotaDto>> GetQuotasAsync(CancellationToken ct = default);
    Task<PassQuotaDto> SetQuotaAsync(SetPassQuotaRequest request, CancellationToken ct = default);
}
