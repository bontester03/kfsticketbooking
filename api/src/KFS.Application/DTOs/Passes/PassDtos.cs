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
    string? Gate = null,
    // Roster-generated passes — drives the per-pass "Sent at X" / "Resend" UI.
    string? IssuedToEmail = null,
    bool EmailSent = false,
    DateTime? EmailSentAt = null);

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

/// <summary>Result of /admin/passes/from-roster — admin uploads a name+email XLSX,
/// one QR is generated per row. Emails are NOT sent here — use the dedicated
/// send-emails endpoint as Step 3 of the Upload → Generate → Send flow.</summary>
public record GenerateFromRosterResponse(
    Guid BatchId,
    int RowsRead,
    int Generated,
    int Skipped,
    IReadOnlyList<RosterRowErrorDto> Errors);

public record RosterRowErrorDto(int RowNumber, string Field, string Message);

/// <summary>Step 1 dry-run preview. Parses the XLSX, dedups against existing passes
/// of (event, type) by IssuedToEmail, returns what would happen if the admin
/// confirmed. No DB changes.</summary>
public record RosterPreviewDto(
    int TotalRows,
    int WouldImport,
    int WouldSkipDuplicates,
    int ErrorRows,
    int QuotaCapacity,
    int QuotaIssued,
    int QuotaRemaining,
    IReadOnlyList<RosterPreviewRowDto> Rows,
    IReadOnlyList<RosterRowErrorDto> Errors);

public record RosterPreviewRowDto(int RowNumber, string FullName, string Email, bool IsDuplicate);

/// <summary>Bulk send-emails result for a whole batch. Each call only re-sends
/// passes where EmailSent = false unless force = true.</summary>
public record SendBatchEmailsResponse(
    Guid BatchId,
    int TotalInBatch,
    int Sent,
    int Skipped,
    int Failed);

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
