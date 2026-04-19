namespace Ambev.DeveloperEvaluation.Domain.Events;

public interface IDomainEvent
{
    DateTime OccurredAt { get; }

    /// <summary>
    /// Schema version of this event. Increment when adding non-optional fields so the
    /// OutboxProcessor can route old messages to a compatible deserializer instead of
    /// silently losing data or throwing on missing required properties.
    /// Current contract: Version = 1 for all initial events.
    /// </summary>
    int Version { get; }
}
