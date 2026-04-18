namespace Ambev.DeveloperEvaluation.Domain.Events;

public record SaleCreatedEvent(Guid SaleId, string SaleNumber, Guid CustomerId, string CustomerName) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
