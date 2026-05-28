using KFS.Domain.Enums;

namespace KFS.Application.Interfaces;

public record PassPdfEntry(string TicketNumber, int SequenceNumber, byte[] QrPng, int SeatsCount, string? IssuedToName, string? Gate = null);

// One parent seat ticket for the student tickets-bundle PDF.
// Mirrors the web TicketCard exactly: violet category badge, VIP A/B block,
// the booking's seat pair on the Arabic line, and the "ticket is sent to …" receipt.
public record StudentSeatTicketEntry(
    string Group,          // "A" or "B" — drives the category badge + gate
    string Row,
    int Seat,
    string ParentRole,     // "Mother" or "Father"
    string StudentName,
    string StudentEmail,   // shown on the receipt panel
    string TicketNumber,
    string PairLabel,      // both seats of the pair, e.g. "A12 & A11"
    byte[] QrPng);

public interface IPassPdfRenderer
{
    byte[] RenderSheet(AdminPassType type, string eventName, DateTime eventDate, IReadOnlyList<PassPdfEntry> entries);

    /// <summary>Combined PDF for the child: their parent passes plus the guest ticket (if any).</summary>
    byte[] RenderStudentTickets(string eventName, DateTime eventDate, string studentName,
        IReadOnlyList<StudentSeatTicketEntry> seats, PassPdfEntry? guest);
}
