using KFS.Domain.Enums;

namespace KFS.Application.Interfaces;

public record PassPdfEntry(string TicketNumber, int SequenceNumber, byte[] QrPng, int SeatsCount, string? IssuedToName);

public interface IPassPdfRenderer
{
    byte[] RenderSheet(AdminPassType type, string eventName, DateTime eventDate, IReadOnlyList<PassPdfEntry> entries);
}
