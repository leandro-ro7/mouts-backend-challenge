using Ambev.DeveloperEvaluation.Domain.Common;
using Ambev.DeveloperEvaluation.Domain.Exceptions;

namespace Ambev.DeveloperEvaluation.Domain.Entities;

public class SaleItem : BaseEntity
{
    public Guid SaleId { get; private set; }

    // External Identity — no FK to Product domain
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;

    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    // Calculated by domain rules — never set from outside
    public decimal Discount { get; private set; }
    public decimal TotalAmount { get; private set; }

    public bool IsCancelled { get; private set; }

    // Required by EF Core
    private SaleItem() { }

    internal SaleItem(Guid saleId, Guid productId, string productName, int quantity, decimal unitPrice)
    {
        SaleId = saleId;
        ProductId = productId;
        ProductName = productName;
        Apply(quantity, unitPrice);
    }

    internal void Update(int quantity, decimal unitPrice)
    {
        Apply(quantity, unitPrice);
    }

    internal void Cancel()
    {
        IsCancelled = true;
    }

    private void Apply(int quantity, decimal unitPrice)
    {
        Quantity = quantity;
        UnitPrice = unitPrice;
        Discount = CalculateDiscount(quantity);
        TotalAmount = Math.Round(quantity * unitPrice * (1 - Discount), 2);
    }

    /// <summary>
    /// Quantity-based discount tiers.
    /// Decision: summary section prevails over narrative text.
    /// - qty 1-3:   0%  (below 4 — no discount allowed)
    /// - qty 4-9:  10%  ("4+" in summary is inclusive at lower bound)
    /// - qty 10-20: 20% (inclusive at both bounds per summary notation)
    /// - qty > 20: throws — business restriction
    /// </summary>
    public static decimal CalculateDiscount(int quantity)
    {
        if (quantity > 20)
            throw new DomainException($"Cannot sell more than 20 identical items. Requested: {quantity}.");

        if (quantity >= 10) return 0.20m;
        if (quantity >= 4)  return 0.10m;
        return 0m;
    }
}
