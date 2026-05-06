using KFS.Domain.Enums;

namespace KFS.Application.Interfaces;

public record QrPayloadInput(
    Guid TicketId,
    Guid EventId,
    ScannedItemType ItemType,
    ZoneCode Zone,
    string? SeatLabel,
    int SeatsCount,
    DateTime ExpiresAt);

public record QrPayloadDecoded(
    Guid TicketId,
    Guid EventId,
    ScannedItemType ItemType,
    ZoneCode Zone,
    string? SeatLabel,
    int SeatsCount,
    DateTime ExpiresAt);

public interface IQrCodeService
{
    string EncodePayload(QrPayloadInput input);
    QrPayloadDecoded DecodePayload(string token);
    byte[] RenderPng(string payload, int pixelsPerModule = 8);
}
