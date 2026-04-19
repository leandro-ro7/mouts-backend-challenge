namespace Ambev.DeveloperEvaluation.Domain.Events;

public record ItemAddedEvent(
    Guid SaleId,
    Guid ItemId,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal TotalAmount) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
