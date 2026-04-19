using Ambev.DeveloperEvaluation.ORM;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ambev.DeveloperEvaluation.Infrastructure.Messaging;

/// <summary>
/// Periodically deletes processed OutboxMessages older than RetentionDays.
/// Runs independently of OutboxProcessor to avoid blocking dispatch throughput.
/// </summary>
public class OutboxCleanupJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxCleanupJob> _logger;
    private readonly OutboxOptions _options;

    public OutboxCleanupJob(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxCleanupJob> logger,
        IOptions<OutboxOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(_options.CleanupIntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(interval, stoppingToken);

            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "OutboxCleanupJob encountered an unhandled error.");
            }
        }
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DefaultContext>();

        var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
        int deleted;

        if (db.Database.IsNpgsql())
        {
            deleted = await db.Database.ExecuteSqlInterpolatedAsync(
                $"""DELETE FROM "OutboxMessages" WHERE "ProcessedAt" IS NOT NULL AND "ProcessedAt" < {cutoff}""",
                cancellationToken);
        }
        else
        {
            // InMemory fallback for tests
            var old = await db.OutboxMessages
                .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
                .ToListAsync(cancellationToken);
            db.OutboxMessages.RemoveRange(old);
            deleted = old.Count;
            await db.SaveChangesAsync(cancellationToken);
        }

        if (deleted > 0)
            _logger.LogInformation(
                "OutboxCleanupJob: purged {Count} processed messages older than {Days} days.",
                deleted, _options.RetentionDays);
    }
}
