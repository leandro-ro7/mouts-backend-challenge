namespace Ambev.DeveloperEvaluation.Domain.Events;

public record ItemCancelledEvent(Guid SaleId, Guid ItemId, Guid ProductId, string ProductName) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
