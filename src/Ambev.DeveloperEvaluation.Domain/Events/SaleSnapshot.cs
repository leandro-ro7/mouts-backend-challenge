namespace Ambev.DeveloperEvaluation.Domain.Events;

/// <summary>
/// Point-in-time snapshot of Sale header + items, embedded in SaleModifiedEvent.
/// Captures both Previous and Current state so consumers have the full delta without
/// requiring a separate query. Not a domain entity — exists only for event payloads.
/// </summary>
public record SaleSnapshot(
    Guid CustomerId,
    string CustomerName,
    Guid BranchId,
    string BranchName,
    DateTime SaleDate,
    decimal TotalAmount,
    IReadOnlyList<SaleItemSnapshot> Items);
