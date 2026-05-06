using KFS.Application.Interfaces;
using KFS.Application.Services;
using KFS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KFS.Api.Jobs;

public class CartSweeperJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CartSweeperJob> _log;

    public CartSweeperJob(IServiceProvider services, ILogger<CartSweeperJob> log)
    {
        _services = services; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var period = TimeSpan.FromSeconds(30);
        using var timer = new PeriodicTimer(period);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var notifier = scope.ServiceProvider.GetRequiredService<ISeatNotifier>();
                var now = DateTime.UtcNow;

                var expiredCarts = await db.Bookings
                    .Where(b => b.Status == BookingStatus.Cart
                        && b.Items.Any(i => i.HoldExpiresAt < now))
                    .Include(b => b.Items).ThenInclude(i => i.Zone)
                    .ToListAsync(stoppingToken);

                foreach (var b in expiredCarts)
                {
                    b.Status = BookingStatus.Expired;
                    foreach (var item in b.Items)
                        await notifier.SeatChangedAsync(new SeatNotification(
                            b.EventId, b.GroupChosen, item.Zone?.Side ?? ZoneSide.None,
                            item.SeatId, "released"), stoppingToken);
                }
                if (expiredCarts.Count > 0) await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogError(ex, "CartSweeper failed"); }
        }
    }
}

public class RebookWindowJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<RebookWindowJob> _log;

    public RebookWindowJob(IServiceProvider services, ILogger<RebookWindowJob> log)
    {
        _services = services; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var now = DateTime.UtcNow;
                var expired = await db.Bookings
                    .Where(b => b.Status == BookingStatus.RebookWindow
                                && b.RebookWindowExpiresAt != null
                                && b.RebookWindowExpiresAt < now)
                    .ToListAsync(stoppingToken);
                foreach (var b in expired) b.Status = BookingStatus.Cancelled;
                if (expired.Count > 0) await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogError(ex, "RebookWindow job failed"); }
        }
    }
}

public class DayBeforeReminderJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DayBeforeReminderJob> _log;

    public DayBeforeReminderJob(IServiceProvider services, ILogger<DayBeforeReminderJob> log)
    {
        _services = services; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _services.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IReminderService>();
                var sent = await svc.RunDayBeforeAsync(stoppingToken);
                if (sent > 0) _log.LogInformation("Day-before reminders sent: {Count}", sent);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogError(ex, "DayBeforeReminder job failed"); }
        }
    }
}
