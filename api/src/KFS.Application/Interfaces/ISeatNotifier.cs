using KFS.Domain.Enums;

namespace KFS.Application.Interfaces;

public record SeatNotification(Guid EventId, ZoneGroup Group, ZoneSide Side, Guid SeatId, string Status);

public interface ISeatNotifier
{
    Task SeatChangedAsync(SeatNotification notification, CancellationToken ct = default);
}
