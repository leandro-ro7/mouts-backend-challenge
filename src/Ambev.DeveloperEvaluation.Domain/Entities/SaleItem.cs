using Ambev.DeveloperEvaluation.Domain.Common;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;

namespace Ambev.DeveloperEvaluation.Domain.Entities;

public class SaleItem : BaseEntity
{
    public Guid SaleId { get; private set; }

    // External Identity — no FK to Product domain
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;

    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    // Value Object — encapsulates tier logic and calculation
    public DiscountRate Discount { get; private set; } = DiscountRate.None;
    public decimal TotalAmount { get; private set; }

    public bool IsCancelled { get; private set; }

    // Required by EF Core
    private SaleItem() { }

    internal SaleItem(Guid saleId, Guid productId, string productName, int quantity, decimal unitPrice)
    {
        Id = Guid.NewGuid();
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
        Discount = DiscountRate.For(quantity);
        TotalAmount = Discount.Apply(quantity * unitPrice);
    }
}
