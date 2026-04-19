using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Services;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Ambev.DeveloperEvaluation.Infrastructure.Messaging;

/// <summary>
/// Transactional Outbox processor — polls OutboxMessages and dispatches via IEventPublisher.
///
/// Delivery guarantee: at-least-once.
///   Events are written to OutboxMessages in the same DB transaction as the aggregate.
///   This processor claims, dispatches, and marks processed.
///
/// Concurrency (PostgreSQL): ClaimBatchAsync uses a single atomic SQL statement with
///   FOR UPDATE SKIP LOCKED. Two concurrent instances cannot claim the same row because
///   the UPDATE subquery holds a row-level lock until committed. ClaimId uniquely identifies
///   the batch claimed by this invocation, preventing cross-instance fetch confusion.
///
/// Concurrency (InMemory / tests): falls back to a tracked-entity approach. InMemory is
///   always single-instance, so the race window does not exist in tests.
///
/// Crash recovery: LockedUntil expiry causes abandoned messages to be retried on the
///   next poll or by another instance.
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(30);
    private const int BatchSize = 50;

    // Built once from the Domain assembly — maps FullName → Type, no version/culture fragility.
    private static readonly IReadOnlyDictionary<string, Type> EventTypeRegistry =
        typeof(IDomainEvent).Assembly
            .GetTypes()
            .Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
            .ToDictionary(t => t.FullName!, t => t);

    public OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchPublicAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Outbox processor encountered an unhandled error.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    // Internal so unit tests can invoke a single poll cycle without the while-loop.
    internal async Task ProcessBatchPublicAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DefaultContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var batch = await ClaimBatchAsync(db, cancellationToken);
        if (batch.Count == 0)
            return;

        foreach (var message in batch)
        {
            try
            {
                if (!EventTypeRegistry.TryGetValue(message.EventType, out var eventType))
                {
                    _logger.LogWarning(
                        "Outbox: unknown event type '{Type}' for message {Id}. Skipping permanently.",
                        message.EventType, message.Id);
                    message.ProcessedAt = DateTime.UtcNow;
                    message.LockedUntil = null;
                    message.ClaimId = null;
                    await db.SaveChangesAsync(cancellationToken);
                    continue;
                }

                var domainEvent = (IDomainEvent)JsonSerializer.Deserialize(message.Payload, eventType)!;
                await publisher.PublishAsync(domainEvent, cancellationToken);

                message.ProcessedAt = DateTime.UtcNow;
                message.LockedUntil = null;
                message.ClaimId = null;
                await db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Outbox: dispatched {EventType} ({Id}).", eventType.Name, message.Id);
            }
            catch (Exception ex)
            {
                // Release lock so the next poll (or another instance) can retry.
                message.LockedUntil = null;
                message.ClaimId = null;
                await db.SaveChangesAsync(cancellationToken);
                _logger.LogError(ex, "Outbox: failed to dispatch {Id}. Lock released for retry.", message.Id);
            }
        }
    }

    /// <summary>
    /// Claims a batch of eligible messages atomically.
    ///
    /// PostgreSQL: one UPDATE statement with a FOR UPDATE SKIP LOCKED subquery — fully atomic,
    ///   no race window between instances. ClaimId identifies exactly which rows this instance owns.
    ///
    /// InMemory (tests): tracked-entity approach — safe because the InMemory provider is
    ///   always single-instance and has no concurrent writers.
    /// </summary>
    private static async Task<List<OutboxMessage>> ClaimBatchAsync(
        DefaultContext db, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var lockExpiry = now.Add(LockDuration);
        var claimId = Guid.NewGuid();

        if (db.Database.IsNpgsql())
        {
            // Single atomic statement: UPDATE rows selected by FOR UPDATE SKIP LOCKED.
            // Any row already locked by another instance is skipped — no double-dispatch.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE "OutboxMessages"
                SET "LockedUntil" = {lockExpiry}, "ClaimId" = {claimId}
                WHERE "Id" = ANY(
                    SELECT "Id" FROM "OutboxMessages"
                    WHERE "ProcessedAt" IS NULL
                      AND ("LockedUntil" IS NULL OR "LockedUntil" < {now})
                    ORDER BY "OccurredAt"
                    LIMIT {BatchSize}
                    FOR UPDATE SKIP LOCKED
                )
                """, cancellationToken);

            // Fetch exactly the rows this instance just claimed — identified by ClaimId.
            return await db.OutboxMessages
                .Where(m => m.ClaimId == claimId)
                .OrderBy(m => m.OccurredAt)
                .ToListAsync(cancellationToken);
        }

        // InMemory fallback: non-atomic but race-free in a single-instance test environment.
        var batch = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null
                     && (m.LockedUntil == null || m.LockedUntil < now))
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (batch.Count == 0)
            return batch;

        foreach (var m in batch)
        {
            m.LockedUntil = lockExpiry;
            m.ClaimId = claimId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return batch;
    }
}
