using Ambev.DeveloperEvaluation.ORM.Outbox;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;

namespace Ambev.DeveloperEvaluation.ORM.Interceptors;

public sealed class OutboxInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is IOutboxContext ctx)
            InjectOutboxMessages(ctx);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is IOutboxContext ctx)
            InjectOutboxMessages(ctx);
        return base.SavingChanges(eventData, result);
    }

    private static void InjectOutboxMessages(IOutboxContext ctx)
    {
        var aggregates = ctx.GetTrackedAggregates().ToList();
        var events = aggregates.SelectMany(a => a.DomainEvents).ToList();

        foreach (var aggregate in aggregates)
            aggregate.ClearDomainEvents();

        foreach (var domainEvent in events)
        {
            ctx.AddOutboxMessage(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = domainEvent.GetType().FullName!,
                Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions),
                OccurredAt = domainEvent.OccurredAt,
                EventVersion = domainEvent.Version
            });
        }
    }
}
