namespace Ambev.DeveloperEvaluation.Domain.Events;

public record ItemCancelledEvent(
    Guid SaleId,
    Guid ItemId,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalAmount) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
