namespace Ambev.DeveloperEvaluation.Domain.Events;

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}
