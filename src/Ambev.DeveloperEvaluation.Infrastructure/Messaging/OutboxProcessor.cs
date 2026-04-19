using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Services;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly OutboxOptions _options;

    // Built once from the Domain assembly — maps FullName → Type, no version/culture fragility.
    private static readonly IReadOnlyDictionary<string, Type> EventTypeRegistry =
        typeof(IDomainEvent).Assembly
            .GetTypes()
            .Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
            .ToDictionary(t => t.FullName!, t => t);

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor> logger,
        IOptions<OutboxOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollingInterval = TimeSpan.FromSeconds(_options.PollingIntervalSeconds);

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

            await Task.Delay(pollingInterval, stoppingToken);
        }
    }

    // Internal so unit tests can invoke a single poll cycle without the while-loop.
    internal async Task ProcessBatchPublicAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DefaultContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var batch = await ClaimBatchAsync(db, cancellationToken);

        foreach (var message in batch)
            await ProcessSingleMessageAsync(message, publisher, db, cancellationToken);
    }

    private async Task ProcessSingleMessageAsync(
        OutboxMessage message,
        IEventPublisher publisher,
        DefaultContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!EventTypeRegistry.TryGetValue(message.EventType, out var eventType))
            {
                _logger.LogWarning(
                    "Outbox: unknown event type '{Type}' for message {Id}. Skipping permanently.",
                    message.EventType, message.Id);
                await MarkProcessedAsync(message, db, cancellationToken);
                return;
            }

            IDomainEvent domainEvent;
            try
            {
                domainEvent = (IDomainEvent)JsonSerializer.Deserialize(message.Payload, eventType)!;
            }
            catch (JsonException ex)
            {
                // Payload is permanently malformed — retrying will never succeed.
                _logger.LogError(ex,
                    "Outbox: malformed JSON payload for message {Id} (type '{Type}'). Skipping permanently.",
                    message.Id, message.EventType);
                await MarkProcessedAsync(message, db, cancellationToken);
                return;
            }

            // Version guard: mismatch means the message was written before a schema upgrade.
            // Still dispatch (may be backwards-compatible) but warn operators.
            if (message.EventVersion != domainEvent.Version)
            {
                _logger.LogWarning(
                    "Outbox: version mismatch for message {Id} — stored EventVersion={StoredVersion}, " +
                    "current {EventType} Version={CurrentVersion}. " +
                    "The payload was written with an older schema; verify backwards compatibility.",
                    message.Id, message.EventVersion, eventType.Name, domainEvent.Version);
            }

            await publisher.PublishAsync(domainEvent, cancellationToken);
            await MarkProcessedAsync(message, db, cancellationToken);

            _logger.LogInformation(
                "Outbox: dispatched {EventType} v{Version} ({Id}).",
                eventType.Name, domainEvent.Version, message.Id);
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

    private static async Task MarkProcessedAsync(
        OutboxMessage message, DefaultContext db, CancellationToken cancellationToken)
    {
        message.ProcessedAt = DateTime.UtcNow;
        message.LockedUntil = null;
        message.ClaimId = null;
        await db.SaveChangesAsync(cancellationToken);
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
    private async Task<List<OutboxMessage>> ClaimBatchAsync(
        DefaultContext db, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var lockExpiry = now.Add(TimeSpan.FromSeconds(_options.LockDurationSeconds));
        var claimId = Guid.NewGuid();
        var batchSize = _options.BatchSize;

        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE "OutboxMessages"
                SET "LockedUntil" = {lockExpiry}, "ClaimId" = {claimId}
                WHERE "Id" = ANY(
                    SELECT "Id" FROM "OutboxMessages"
                    WHERE "ProcessedAt" IS NULL
                      AND ("LockedUntil" IS NULL OR "LockedUntil" < {now})
                    ORDER BY "OccurredAt"
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED
                )
                """, cancellationToken);

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
            .Take(batchSize)
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
