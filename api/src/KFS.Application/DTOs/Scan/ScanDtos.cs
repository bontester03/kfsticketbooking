using KFS.Domain.Enums;

namespace KFS.Application.DTOs.Scan;

public record ScanRequest(string QrPayload, string EventToken, string? DeviceInfo);

// One row of the admin scan-audit: a ticket and whether/when it was scanned.
public record ScanAuditRow(
    string Kind,            // VVIP / Guest / Staff / Media / Seat
    string TicketNumber,
    string? Holder,         // issued-to name, or "Mother of <student>" for seats
    string? Detail,         // zone / seat label
    int SeatsCount,         // admissions this ticket allows (Guest = 3)
    int AdmittedCount,      // valid scans so far
    bool Scanned,
    DateTime? FirstScannedAt,
    DateTime? LastScannedAt);

public record ScanAuditDto(
    int TotalTickets,
    int ScannedTickets,
    int AdmittedPeople,
    IReadOnlyList<ScanAuditRow> Rows);

public record ScanResponse(
    bool Valid,
    ScanResult Result,
    ScannedItemType? ItemType,
    string? Zone,
    string? SeatLabel,
    int? SeatsCount,        // total admissions this ticket allows (Guest = 3, others = 1)
    int AdmittedCount,      // admissions used so far, including this scan when Valid
    string? HolderName,
    bool AlreadyScanned,
    DateTime? FirstScannedAt,
    string? Message);
