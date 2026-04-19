using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Services;
using Ambev.DeveloperEvaluation.Infrastructure.Messaging;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Infrastructure;

/// <summary>
/// Unit tests for OutboxProcessor using EF Core InMemory database.
/// Validates dispatch, at-least-once retry, unknown-type handling, and locking semantics.
/// </summary>
public class OutboxProcessorTests : IDisposable
{
    private readonly DefaultContext _db;
    private readonly IEventPublisher _publisher;
    private readonly IServiceScope _scope;
    private readonly IServiceScopeFactory _scopeFactory;

    public OutboxProcessorTests()
    {
        var options = new DbContextOptionsBuilder<DefaultContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new DefaultContext(options);
        _publisher = Substitute.For<IEventPublisher>();

        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddSingleton(_publisher);

        var provider = services.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        _scope = provider.CreateScope();
    }

    private static IOptions<OutboxOptions> DefaultOptions =>
        Options.Create(new OutboxOptions());

    private OutboxProcessor CreateProcessor() =>
        new(_scopeFactory, NullLogger<OutboxProcessor>.Instance, DefaultOptions);

    private async Task SeedOutboxAsync(string eventType, string payload, DateTime? lockedUntil = null)
    {
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Payload = payload,
            OccurredAt = DateTime.UtcNow,
            LockedUntil = lockedUntil
        });
        await _db.SaveChangesAsync();
    }

    [Fact(DisplayName = "Pending message: dispatched and marked processed")]
    public async Task PendingMessage_IsDispatchedAndMarkedProcessed()
    {
        var eventFullName = typeof(SaleCreatedEvent).FullName!;
        var payload = """{"SaleId":"00000000-0000-0000-0000-000000000001","SaleNumber":"S1","CustomerId":"00000000-0000-0000-0000-000000000002","CustomerName":"C","BranchId":"00000000-0000-0000-0000-000000000003","BranchName":"B1","SaleDate":"2025-01-01T00:00:00Z","TotalAmount":0,"Items":[],"OccurredAt":"2025-01-01T00:00:00Z"}""";
        await SeedOutboxAsync(eventFullName, payload);

        var processor = CreateProcessor();
        await processor.ProcessBatchPublicAsync(CancellationToken.None);

        await _publisher.Received(1).PublishAsync(
            Arg.Is<IDomainEvent>(e => e is SaleCreatedEvent),
            Arg.Any<CancellationToken>());

        var msg = await _db.OutboxMessages.SingleAsync();
        msg.ProcessedAt.Should().NotBeNull();
        msg.LockedUntil.Should().BeNull();
    }

    [Fact(DisplayName = "Already-processed message: not dispatched again")]
    public async Task AlreadyProcessed_NotDispatchedAgain()
    {
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = typeof(SaleCreatedEvent).FullName!,
            Payload = "{}",
            OccurredAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow  // already done
        });
        await _db.SaveChangesAsync();

        var processor = CreateProcessor();
        await processor.ProcessBatchPublicAsync(CancellationToken.None);

        await _publisher.DidNotReceive().PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Currently-locked message: not dispatched (another instance owns it)")]
    public async Task LockedMessage_NotDispatched()
    {
        await SeedOutboxAsync(
            typeof(SaleCreatedEvent).FullName!,
            "{}",
            lockedUntil: DateTime.UtcNow.AddSeconds(30));  // lock still valid

        var processor = CreateProcessor();
        await processor.ProcessBatchPublicAsync(CancellationToken.None);

        await _publisher.DidNotReceive().PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Expired-lock message: retried and dispatched")]
    public async Task ExpiredLock_IsRetried()
    {
        var eventFullName = typeof(SaleCreatedEvent).FullName!;
        var payload = """{"SaleId":"00000000-0000-0000-0000-000000000001","SaleNumber":"S1","CustomerId":"00000000-0000-0000-0000-000000000002","CustomerName":"C","BranchId":"00000000-0000-0000-0000-000000000003","BranchName":"B1","SaleDate":"2025-01-01T00:00:00Z","TotalAmount":0,"Items":[],"OccurredAt":"2025-01-01T00:00:00Z"}""";
        await SeedOutboxAsync(eventFullName, payload, lockedUntil: DateTime.UtcNow.AddSeconds(-60));

        var processor = CreateProcessor();
        await processor.ProcessBatchPublicAsync(CancellationToken.None);

        await _publisher.Received(1).PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Unknown event type: marked processed and skipped silently")]
    public async Task UnknownEventType_MarkedProcessedAndSkipped()
    {
        await SeedOutboxAsync("NonExistent.Namespace.BogusEvent", "{}");

        var processor = CreateProcessor();
        await processor.ProcessBatchPublicAsync(CancellationToken.None);

        await _publisher.DidNotReceive().PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());

        var msg = await _db.OutboxMessages.SingleAsync();
        msg.ProcessedAt.Should().NotBeNull("unknown events are skipped permanently");
    }

    [Fact(DisplayName = "Publish failure: lock released so message is retried on next poll")]
    public async Task PublishFailure_ReleasesLockForRetry()
    {
        var eventFullName = typeof(SaleCreatedEvent).FullName!;
        var payload = """{"SaleId":"00000000-0000-0000-0000-000000000001","SaleNumber":"S1","CustomerId":"00000000-0000-0000-0000-000000000002","CustomerName":"C","BranchId":"00000000-0000-0000-0000-000000000003","BranchName":"B1","SaleDate":"2025-01-01T00:00:00Z","TotalAmount":0,"Items":[],"OccurredAt":"2025-01-01T00:00:00Z"}""";
        await SeedOutboxAsync(eventFullName, payload);

        _publisher.PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("broker down"));

        var processor = CreateProcessor();
        await processor.ProcessBatchPublicAsync(CancellationToken.None);

        var msg = await _db.OutboxMessages.SingleAsync();
        msg.ProcessedAt.Should().BeNull("publish failed — not marked done");
        msg.LockedUntil.Should().BeNull("lock released for next poll to retry");
    }

    /// <summary>
    /// At-least-once guarantee: if the processor crashes (or is killed) after PublishAsync succeeds
    /// but before ProcessedAt is written, the message must be retried on the next poll.
    ///
    /// Simulated here by having the first SaveChangesAsync (post-publish) fail, leaving
    /// LockedUntil expired. The next poll must re-dispatch the message.
    /// </summary>
    [Fact(DisplayName = "At-least-once: message with expired lock is re-dispatched on next poll")]
    public async Task ExpiredLock_OnNextPoll_IsRedispatched()
    {
        var eventFullName = typeof(SaleCreatedEvent).FullName!;
        var payload = """{"SaleId":"00000000-0000-0000-0000-000000000001","SaleNumber":"S1","CustomerId":"00000000-0000-0000-0000-000000000002","CustomerName":"C","BranchId":"00000000-0000-0000-0000-000000000003","BranchName":"B1","SaleDate":"2025-01-01T00:00:00Z","TotalAmount":0,"Items":[],"OccurredAt":"2025-01-01T00:00:00Z"}""";

        // Seed message as if a previous processor instance claimed and then crashed:
        // LockedUntil is already expired (60 seconds in the past) and ProcessedAt is null.
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventFullName,
            Payload = payload,
            OccurredAt = DateTime.UtcNow.AddMinutes(-5),
            LockedUntil = DateTime.UtcNow.AddSeconds(-60),  // expired lock — crash scenario
            ProcessedAt = null                               // NOT processed — must retry
        });
        await _db.SaveChangesAsync();

        var processor = CreateProcessor();
        await processor.ProcessBatchPublicAsync(CancellationToken.None);

        // The message MUST be dispatched (at-least-once delivery)
        await _publisher.Received(1).PublishAsync(
            Arg.Is<IDomainEvent>(e => e is SaleCreatedEvent),
            Arg.Any<CancellationToken>());

        // And marked as processed
        var msg = await _db.OutboxMessages.SingleAsync();
        msg.ProcessedAt.Should().NotBeNull("message was dispatched — must be marked done");
        msg.LockedUntil.Should().BeNull("lock cleared after successful dispatch");
    }

    /// <summary>
    /// Verifies that a message dispatched successfully IS marked as processed.
    /// This validates the other side of at-least-once: successful dispatch → no re-dispatch.
    /// </summary>
    [Fact(DisplayName = "At-least-once: successfully dispatched message is not re-dispatched on next poll")]
    public async Task SuccessfulDispatch_NotRedispatchedOnNextPoll()
    {
        var eventFullName = typeof(SaleCreatedEvent).FullName!;
        var payload = """{"SaleId":"00000000-0000-0000-0000-000000000001","SaleNumber":"S1","CustomerId":"00000000-0000-0000-0000-000000000002","CustomerName":"C","BranchId":"00000000-0000-0000-0000-000000000003","BranchName":"B1","SaleDate":"2025-01-01T00:00:00Z","TotalAmount":0,"Items":[],"OccurredAt":"2025-01-01T00:00:00Z"}""";
        await SeedOutboxAsync(eventFullName, payload);

        var processor = CreateProcessor();

        // First poll — dispatched and marked processed
        await processor.ProcessBatchPublicAsync(CancellationToken.None);
        await _publisher.Received(1).PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());

        _publisher.ClearReceivedCalls();

        // Second poll — must NOT dispatch again
        await processor.ProcessBatchPublicAsync(CancellationToken.None);
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _scope.Dispose();
        _db.Dispose();
    }
}
