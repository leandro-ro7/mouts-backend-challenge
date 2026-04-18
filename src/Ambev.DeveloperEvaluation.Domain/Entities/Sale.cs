using Ambev.DeveloperEvaluation.Domain.Common;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;

namespace Ambev.DeveloperEvaluation.Domain.Entities;

public class Sale : AggregateRoot
{
    // Generated server-side — never accepted as client input
    public string SaleNumber { get; private set; } = string.Empty;
    public DateTime SaleDate { get; private set; }

    // External Identities — denormalized snapshots, no FK to User or Branch domains
    public Guid CustomerId { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public Guid BranchId { get; private set; }
    public string BranchName { get; private set; } = string.Empty;

    // Recalculated whenever items change — never stored as input
    public decimal TotalAmount { get; private set; }

    // Soft-delete — sale records are immutable historical data
    public bool IsCancelled { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<SaleItem> _items = new();
    public IReadOnlyList<SaleItem> Items => _items.AsReadOnly();

    // Required by EF Core
    private Sale() { }

    public static Sale Create(
        Guid customerId, string customerName,
        Guid branchId, string branchName,
        DateTime saleDate)
    {
        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            SaleNumber = GenerateSaleNumber(),
            CustomerId = customerId,
            CustomerName = customerName,
            BranchId = branchId,
            BranchName = branchName,
            SaleDate = saleDate,
            IsCancelled = false,
            CreatedAt = DateTime.UtcNow
        };

        sale.RaiseDomainEvent(new SaleCreatedEvent(sale.Id, sale.SaleNumber, customerId, customerName));
        return sale;
    }

    public SaleItem AddItem(Guid productId, string productName, int quantity, decimal unitPrice)
    {
        if (IsCancelled)
            throw new DomainException("Cannot add items to a cancelled sale.");

        var item = new SaleItem(Id, productId, productName, quantity, unitPrice);
        _items.Add(item);
        RecalculateTotal();
        return item;
    }

    public void UpdateItem(Guid itemId, int quantity, decimal unitPrice)
    {
        if (IsCancelled)
            throw new DomainException("Cannot update items of a cancelled sale.");

        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new DomainException($"Item {itemId} not found in sale {SaleNumber}.");

        if (item.IsCancelled)
            throw new DomainException("Cannot update a cancelled item.");

        item.Update(quantity, unitPrice);
        RecalculateTotal();
        UpdatedAt = DateTime.UtcNow;
    }

    public SaleItem CancelItem(Guid itemId)
    {
        if (IsCancelled)
            throw new DomainException("Cannot cancel an item of an already cancelled sale.");

        var item = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new DomainException($"Item {itemId} not found in sale {SaleNumber}.");

        if (item.IsCancelled)
            throw new DomainException($"Item {itemId} is already cancelled.");

        item.Cancel();
        RecalculateTotal();
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new ItemCancelledEvent(Id, item.Id, item.ProductId, item.ProductName));
        return item;
    }

    public void Cancel()
    {
        if (IsCancelled)
            throw new DomainException($"Sale {SaleNumber} is already cancelled.");

        IsCancelled = true;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new SaleCancelledEvent(Id, SaleNumber));
    }

    public void Update(Guid customerId, string customerName, Guid branchId, string branchName, DateTime saleDate)
    {
        if (IsCancelled)
            throw new DomainException("Cannot update a cancelled sale.");

        CustomerId = customerId;
        CustomerName = customerName;
        BranchId = branchId;
        BranchName = branchName;
        SaleDate = saleDate;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new SaleModifiedEvent(Id, SaleNumber));
    }

    public void ReplaceItems(IEnumerable<NewSaleItemSpec> newItems)
    {
        if (IsCancelled)
            throw new DomainException("Cannot update items of a cancelled sale.");

        // Soft-cancel existing active items directly (bypasses CancelItem to avoid ItemCancelledEvent —
        // this is a replace operation, not a business cancellation of individual items)
        foreach (var item in _items.Where(i => !i.IsCancelled))
            item.Cancel();

        foreach (var spec in newItems)
            _items.Add(new SaleItem(Id, spec.ProductId, spec.ProductName, spec.Quantity, spec.UnitPrice));

        RecalculateTotal();
        UpdatedAt = DateTime.UtcNow;
    }

    private void RecalculateTotal()
    {
        TotalAmount = _items
            .Where(i => !i.IsCancelled)
            .Sum(i => i.TotalAmount);
    }

    private static string GenerateSaleNumber() =>
        $"{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
}
