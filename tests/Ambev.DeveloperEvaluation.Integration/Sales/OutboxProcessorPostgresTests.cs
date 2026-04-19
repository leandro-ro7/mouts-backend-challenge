using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Services;
using Ambev.DeveloperEvaluation.Infrastructure.Messaging;
using Ambev.DeveloperEvaluation.Integration.Infrastructure;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Json;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Sales;

/// <summary>
/// Validates OutboxProcessor behaviour that requires a real PostgreSQL instance:
/// - FOR UPDATE SKIP LOCKED: two concurrent processors do not dispatch the same message
/// - At-least-once: lock expiry re-exposes a message to the next poll
/// - Processed messages are not re-dispatched
/// </summary>
[Collection("PostgreSql")]
public class OutboxProcessorPostgresTests
{
    private readonly PostgreSqlFixture _fixture;

    public OutboxProcessorPostgresTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private static OutboxMessage MakeMessage(string? payload = null) => new()
    {
        Id = Guid.NewGuid(),
        EventType = typeof(SaleCancelledEvent).FullName!,
        Payload = payload ?? JsonSerializer.Serialize(new SaleCancelledEvent(
            Guid.NewGuid(), "S-001",
            Guid.NewGuid(), "C", Guid.NewGuid(), "B", 100m)),
        OccurredAt = DateTime.UtcNow
    };

    private OutboxProcessor BuildProcessor(DefaultContext ctx, IEventPublisher publisher, int batchSize = 50)
    {
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        services.AddSingleton(publisher);
        var provider = services.BuildServiceProvider();

        var factory = Substitute.For<IServiceScopeFactory>();
        var scope   = Substitute.For<IServiceScope>();
        factory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(provider);

        var options = Options.Create(new OutboxOptions
        {
            LockDurationSeconds    = 30,
            BatchSize              = batchSize,
            PollingIntervalSeconds = 5
        });

        return new OutboxProcessor(factory, NullLogger<OutboxProcessor>.Instance, options);
    }

    /// <summary>
    /// Validates that FOR UPDATE SKIP LOCKED (or the LockedUntil write-ahead fence) prevents
    /// two concurrent processors from dispatching the same message.
    ///
    /// Design rationale:
    ///   BatchSize = 1 → each processor claims at most one row per poll cycle.
    ///   Gate (TaskCompletionSource) → procA blocks inside PublishAsync until procB has also
    ///     reached PublishAsync, meaning both ClaimBatch UPDATEs have committed before either
    ///     processor advances. This forces the overlap window that SKIP LOCKED is designed to handle:
    ///     while procA holds LockedUntil on row1, procB's subquery sees row1 as ineligible and
    ///     claims row2 instead.
    ///   Result: each processor dispatches exactly one distinct message — no double-dispatch.
    /// </summary>
    [Fact(DisplayName = "FOR UPDATE SKIP LOCKED: two processors with overlapping claims each dispatch exactly one distinct message")]
    public async Task TwoConcurrentProcessors_EachDispatchExactlyOneDistinctMessage()
    {
        await using var seedCtx = _fixture.CreateContext();
        var msgA = MakeMessage();
        var msgB = MakeMessage();
        // Ensure deterministic OccurredAt ordering so both rows are eligible immediately
        msgA.OccurredAt = DateTime.UtcNow.AddSeconds(-2);
        msgB.OccurredAt = DateTime.UtcNow.AddSeconds(-1);
        seedCtx.OutboxMessages.AddRange(msgA, msgB);
        await seedCtx.SaveChangesAsync();

        // Gate: opens when both processors have reached PublishAsync — meaning both claims
        // have already committed to the DB. This is the critical overlap window.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int arrivedAtPublish = 0;

        var pubA = Substitute.For<IEventPublisher>();
        pubA.PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                if (Interlocked.Increment(ref arrivedAtPublish) == 2)
                    gate.TrySetResult();
                await gate.Task; // wait until both processors have claimed before completing
            });

        var pubB = Substitute.For<IEventPublisher>();
        pubB.PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                if (Interlocked.Increment(ref arrivedAtPublish) == 2)
                    gate.TrySetResult();
                await gate.Task;
            });

        await using var ctxA = _fixture.CreateContext();
        await using var ctxB = _fixture.CreateContext();

        // BatchSize = 1: each processor claims at most one row, preventing one instance from
        // vacuuming up all messages before the other even starts.
        var procA = BuildProcessor(ctxA, pubA, batchSize: 1);
        var procB = BuildProcessor(ctxB, pubB, batchSize: 1);

        await Task.WhenAll(
            procA.ProcessBatchPublicAsync(CancellationToken.None),
            procB.ProcessBatchPublicAsync(CancellationToken.None));

        // Each processor must have dispatched exactly one event — no processor got both, none got zero
        await pubA.Received(1).PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
        await pubB.Received(1).PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());

        // Both DB rows must be marked processed — no message was lost
        await using var verifyCtx = _fixture.CreateContext();
        var rows = await verifyCtx.OutboxMessages
            .Where(m => m.Id == msgA.Id || m.Id == msgB.Id)
            .ToListAsync();
        rows.Should().AllSatisfy(m => m.ProcessedAt.Should().NotBeNull(
            "every seeded message must be dispatched exactly once and marked done"));
    }

    [Fact(DisplayName = "Processed message is not re-dispatched on subsequent poll")]
    public async Task ProcessedMessage_NotRedispatchedOnNextPoll()
    {
        await using var ctx = _fixture.CreateContext();

        var msg = MakeMessage();
        ctx.OutboxMessages.Add(msg);
        await ctx.SaveChangesAsync();

        var publisher = Substitute.For<IEventPublisher>();
        var processor = BuildProcessor(ctx, publisher);

        // First poll — dispatches and marks processed
        await processor.ProcessBatchPublicAsync(CancellationToken.None);
        // Second poll — nothing eligible
        await processor.ProcessBatchPublicAsync(CancellationToken.None);

        await publisher.Received(1).PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Expired lock re-exposes message to next poll (at-least-once recovery)")]
    public async Task ExpiredLock_MessageRetried()
    {
        await using var ctx = _fixture.CreateContext();

        // Seed a message already locked but with an expired LockedUntil
        var msg = MakeMessage();
        msg.LockedUntil = DateTime.UtcNow.AddSeconds(-10); // already expired
        msg.ClaimId = Guid.NewGuid();
        ctx.OutboxMessages.Add(msg);
        await ctx.SaveChangesAsync();

        var publisher = Substitute.For<IEventPublisher>();
        var processor = BuildProcessor(ctx, publisher);

        await processor.ProcessBatchPublicAsync(CancellationToken.None);

        await publisher.Received(1).PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());

        var processed = await ctx.OutboxMessages.FirstAsync(m => m.Id == msg.Id);
        processed.ProcessedAt.Should().NotBeNull("expired lock should have allowed re-claim and processing");
    }
}
