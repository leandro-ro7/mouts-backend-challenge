namespace Ambev.DeveloperEvaluation.Domain.Events;

public record SaleCreatedEvent(
    Guid SaleId,
    string SaleNumber,
    Guid CustomerId,
    string CustomerName,
    Guid BranchId,
    string BranchName,
    DateTime SaleDate,
    decimal TotalAmount,
    IReadOnlyList<SaleItemSnapshot> Items) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
