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
}
