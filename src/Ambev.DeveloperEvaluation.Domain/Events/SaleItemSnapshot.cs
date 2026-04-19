namespace Ambev.DeveloperEvaluation.Domain.Events;

/// <summary>
/// Immutable point-in-time snapshot of a SaleItem embedded inside domain events.
/// Not a domain entity — exists only to make event payloads self-contained.
/// </summary>
public record SaleItemSnapshot(
    Guid ItemId,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal TotalAmount);
