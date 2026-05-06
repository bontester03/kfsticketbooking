using KFS.Application.Interfaces;
using KFS.Domain.Enums;
using Microsoft.AspNetCore.SignalR;

namespace KFS.Api.Hubs;

public class SeatMapHub : Hub
{
    public Task JoinGroup(Guid eventId, ZoneGroup group)
        => Groups.AddToGroupAsync(Context.ConnectionId, GroupKey(eventId, group));

    public Task LeaveGroup(Guid eventId, ZoneGroup group)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupKey(eventId, group));

    public static string GroupKey(Guid eventId, ZoneGroup group) => $"evt-{eventId}-{group}";
}

public class SignalRSeatNotifier : ISeatNotifier
{
    private readonly IHubContext<SeatMapHub> _hub;
    public SignalRSeatNotifier(IHubContext<SeatMapHub> hub) => _hub = hub;

    public Task SeatChangedAsync(SeatNotification n, CancellationToken ct = default)
        => _hub.Clients.Group(SeatMapHub.GroupKey(n.EventId, n.Group)).SendAsync("seat-changed", new
        {
            eventId = n.EventId, group = n.Group.ToString(), side = n.Side.ToString(),
            seatId = n.SeatId, status = n.Status
        }, ct);
}
