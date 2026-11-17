using backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace backend.BackgroundJobs
{
    public class ArchiveEventsJob(IServiceScopeFactory scopeFactory, ILogger<ArchiveEventsJob> logger) : BackgroundService
    {
        private static readonly TimeSpan RunAtUtc = new(3, 0, 0); // 3:00 AM UTC
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(GetDelayUntilNextRun(), stoppingToken);

                    await using var scope = scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    db.Database.SetCommandTimeout(TimeSpan.FromMinutes(30));

                    long archived = await db.Database
                        .SqlQuery<long>($"""SELECT archive_events(NULL::timestamptz) AS "Value" """)
                        .SingleAsync(stoppingToken);

                    logger.LogInformation("Archived {ArchivedCount} events", archived);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break; // apagado normal
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "ArchiveEventsJob failed; will retry at the next scheduled run");
                    // Evita un bucle caliente si el fallo ocurre justo en la hora de ejecución
                    try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }

        private static TimeSpan GetDelayUntilNextRun()
        {
            var now = DateTime.UtcNow;
            var next = now.Date + RunAtUtc;
            if (next <= now) next = next.AddDays(1);
            return next - now;
        }
    }
}
