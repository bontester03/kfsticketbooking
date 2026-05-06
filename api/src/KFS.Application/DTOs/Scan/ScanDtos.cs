using KFS.Domain.Enums;

namespace KFS.Application.DTOs.Scan;

public record ScanRequest(string QrPayload, string EventToken, string? DeviceInfo);

public record ScanResponse(
    bool Valid,
    ScanResult Result,
    ScannedItemType? ItemType,
    string? Zone,
    string? SeatLabel,
    int? SeatsCount,
    string? HolderName,
    bool AlreadyScanned,
    DateTime? FirstScannedAt,
    string? Message);
