namespace Ambev.DeveloperEvaluation.Domain.Events;

public record SaleModifiedEvent(
    Guid SaleId,
    string SaleNumber,
    // Previous snapshot — captured before mutation
    Guid PreviousCustomerId,
    string PreviousCustomerName,
    Guid PreviousBranchId,
    string PreviousBranchName,
    DateTime PreviousSaleDate,
    decimal PreviousTotalAmount,
    // New values
    Guid NewCustomerId,
    string NewCustomerName,
    Guid NewBranchId,
    string NewBranchName,
    DateTime NewSaleDate,
    decimal NewTotalAmount) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public int Version { get; } = 1;
}
