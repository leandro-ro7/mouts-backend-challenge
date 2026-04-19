namespace Ambev.DeveloperEvaluation.Domain.Events;

public record ItemModifiedEvent(
    Guid SaleId,
    Guid ItemId,
    Guid ProductId,
    string ProductName,
    int PreviousQuantity,
    decimal PreviousUnitPrice,
    int NewQuantity,
    decimal NewUnitPrice,
    decimal NewDiscount,
    decimal NewTotalAmount) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
