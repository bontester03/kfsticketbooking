using KFS.Domain.Enums;

namespace KFS.Application.DTOs.Passes;

public record GeneratePassesRequest(AdminPassType Type, int Count, PassOutputFormat Format);

public record GeneratePassesResponse(Guid BatchId, int Count, string DownloadUrl, PassOutputFormat Format);

public record AdminPassDto(
    Guid Id,
    Guid BatchId,
    AdminPassType Type,
    int SequenceNumber,
    string TicketNumber,
    int SeatsCount,
    string? IssuedToName,
    string? QrCodeImageUrl,
    DateTime IssuedAt);

public record PassBatchSummaryDto(
    Guid BatchId,
    AdminPassType Type,
    int Count,
    int SeatsTotal,
    DateTime CreatedAt,
    string? PdfUrl,
    string? ZipUrl);

public record UpdatePassRequest(string? IssuedToName);
