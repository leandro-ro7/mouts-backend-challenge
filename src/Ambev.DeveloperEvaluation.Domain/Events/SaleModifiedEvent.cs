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
    // New values
    Guid NewCustomerId,
    string NewCustomerName,
    Guid NewBranchId,
    string NewBranchName,
    DateTime NewSaleDate) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
