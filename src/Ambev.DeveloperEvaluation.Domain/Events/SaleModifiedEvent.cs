namespace Ambev.DeveloperEvaluation.Domain.Events;

public record SaleModifiedEvent(Guid SaleId, string SaleNumber) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
