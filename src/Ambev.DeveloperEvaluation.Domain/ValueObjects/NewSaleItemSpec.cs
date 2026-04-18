namespace Ambev.DeveloperEvaluation.Domain.ValueObjects;

public sealed record NewSaleItemSpec(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);
