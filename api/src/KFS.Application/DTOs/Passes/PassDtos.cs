using KFS.Domain.Enums;

namespace KFS.Application.DTOs.Passes;

public record GeneratePassesRequest(Guid EventId, AdminPassType Type, int Count, PassOutputFormat Format);

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
    DateTime IssuedAt,
    int AdmittedCount = 0,
    // For student-linked Guest passes: "Gate A" or "Gate B" matching the child's VIP booking.
    // Null for unlinked pool passes (UI falls back to the per-type default gate label).
    string? Gate = null);

public record PassBatchSummaryDto(
    Guid BatchId,
    AdminPassType Type,
    int Count,
    int SeatsTotal,
    DateTime CreatedAt,
    string? PdfUrl,
    string? ZipUrl,
    int ScannedPasses = 0);

public record UpdatePassRequest(string? IssuedToName);

// Per-type quota: Capacity is the configured limit (zone capacity), Issued is the number of
// seats already generated, Remaining = Capacity - Issued.
public record PassQuotaDto(AdminPassType Type, string Label, int Capacity, int Issued, int Remaining);

public record SetPassQuotaRequest(Guid EventId, AdminPassType Type, int Capacity);

// A Guest ticket tied to a child (1 QR admits 3). AdmittedCount = valid scans so far.
public record GuestPassDto(
    Guid Id,
    string TicketNumber,
    int SeatsCount,
    int AdmittedCount,
    bool FullyUsed,
    string? QrCodeImageUrl,
    Guid? StudentId,
    string? StudentName,
    bool IssuedByAdmin,
    DateTime IssuedAt,
    // "Gate A" if the child has a VIP A booking, "Gate B" for VIP B, otherwise "Gate A" (default).
    string Gate = "Gate A");

public record IssueGuestToStudentRequest(Guid StudentId, string? IssuedToName);

public record GuestEligibleStudentDto(Guid Id, string FullName, string Email, bool HasGuestPass);

// Aggregate guest-pass analytics for the admin area.
public record GuestAnalyticsDto(
    int Limit,
    int Issued,
    int Remaining,
    int PassesTotal,
    int BookedByStudents,
    int IssuedByAdminToChild,
    int UnassignedPool,
    int AdmittedPeople);
