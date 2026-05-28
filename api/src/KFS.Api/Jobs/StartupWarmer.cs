using KFS.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace KFS.Api.Jobs;

/// <summary>
/// Pre-warms the hot paths so the first ~50 user requests after a deploy don't pay the
/// JIT-compilation tax. Without this the first BCrypt verify on /auth/login costs ~500 ms
/// (BCrypt itself plus the JIT cost); with it, that tax is paid once at startup off the
/// request thread. Particularly important for the 300-parent simultaneous login burst.
///
/// Runs as a fire-and-forget background task so the host can serve requests immediately.
/// </summary>
public class StartupWarmer : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<StartupWarmer> _log;

    public StartupWarmer(IServiceProvider services, ILogger<StartupWarmer> log)
    {
        _services = services; _log = log;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _ = Task.Run(async () =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var scope = _services.CreateScope();
                var sp = scope.ServiceProvider;

                // 1. Warm BCrypt JIT (this is the biggest cost on the first /auth/login).
                var hasher = sp.GetRequiredService<IPasswordHasher>();
                var hash = hasher.Hash("startup_warmup_throwaway_value");
                hasher.Verify("startup_warmup_throwaway_value", hash);

                // 2. Warm EF Core: force compilation of the query plans the booking flow hits hot.
                var db = sp.GetRequiredService<IApplicationDbContext>();
                _ = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.IsActive, ct);
                _ = await db.Zones.AsNoTracking().Take(8).ToListAsync(ct);
                _ = await db.Seats.AsNoTracking().Take(20).ToListAsync(ct);
                _ = await db.Students.AsNoTracking().Take(1).ToListAsync(ct);
                _ = await db.AdminPasses.AsNoTracking().Take(1).ToListAsync(ct);

                _log.LogInformation("StartupWarmer finished in {Ms} ms (BCrypt + EF hot paths primed).",
                    sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "StartupWarmer failed (non-fatal — the API still works, " +
                    "the first few requests just pay the JIT cost).");
            }
        }, ct);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
