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

    // Optimistic concurrency token — incremented on every mutation.
    // EF Core adds "AND RowVersion = @original" to UPDATE queries, causing
    // DbUpdateConcurrencyException when a concurrent request already mutated the row.
    public uint RowVersion { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<SaleItem> _items = new();
    public IReadOnlyList<SaleItem> Items => _items.AsReadOnly();

    // Required by EF Core
    private Sale() { }

    public static Sale Create(
        Guid customerId, string customerName,
        Guid branchId, string branchName,
        DateTime saleDate,
        IEnumerable<NewSaleItemSpec> items)
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

        // Add items before raising the event so the snapshot is complete.
        foreach (var spec in items)
            sale._items.Add(new SaleItem(sale.Id, spec.ProductId, spec.ProductName, spec.Quantity, spec.UnitPrice));

        sale.RecalculateTotal();

        var snapshots = sale._items
            .Select(i => new SaleItemSnapshot(i.Id, i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.Discount.Value, i.TotalAmount))
            .ToList()
            .AsReadOnly();

        sale.RaiseDomainEvent(new SaleCreatedEvent(
            sale.Id, sale.SaleNumber,
            customerId, customerName,
            branchId, branchName,
            saleDate, sale.TotalAmount, snapshots));

        return sale;
    }

    public SaleItem AddItem(Guid productId, string productName, int quantity, decimal unitPrice)
    {
        if (IsCancelled)
            throw new DomainException("Cannot add items to a cancelled sale.");

        var item = new SaleItem(Id, productId, productName, quantity, unitPrice);
        _items.Add(item);
        RecalculateTotal();

        RaiseDomainEvent(new ItemAddedEvent(
            Id, item.Id, item.ProductId, item.ProductName,
            item.Quantity, item.UnitPrice, item.Discount.Value, item.TotalAmount));

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

        var previousQuantity = item.Quantity;
        var previousUnitPrice = item.UnitPrice;

        item.Update(quantity, unitPrice);
        RecalculateTotal();
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new ItemModifiedEvent(
            Id, item.Id, item.ProductId, item.ProductName,
            previousQuantity, previousUnitPrice,
            item.Quantity, item.UnitPrice, item.Discount.Value, item.TotalAmount));
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
        RowVersion++;

        RaiseDomainEvent(new ItemCancelledEvent(
            Id, item.Id, item.ProductId, item.ProductName,
            item.Quantity, item.UnitPrice, item.TotalAmount));
        return item;
    }

    public void Cancel()
    {
        if (IsCancelled)
            throw new DomainException($"Sale {SaleNumber} is already cancelled.");

        IsCancelled = true;
        UpdatedAt = DateTime.UtcNow;
        RowVersion++;

        RaiseDomainEvent(new SaleCancelledEvent(
            Id, SaleNumber,
            CustomerId, CustomerName,
            BranchId, BranchName,
            TotalAmount));
    }

    public void Update(Guid customerId, string customerName, Guid branchId, string branchName, DateTime saleDate)
    {
        if (IsCancelled)
            throw new DomainException("Cannot update a cancelled sale.");

        var evt = new SaleModifiedEvent(
            Id, SaleNumber,
            CustomerId, CustomerName, BranchId, BranchName, SaleDate,
            customerId, customerName, branchId, branchName, saleDate);

        CustomerId = customerId;
        CustomerName = customerName;
        BranchId = branchId;
        BranchName = branchName;
        SaleDate = saleDate;
        UpdatedAt = DateTime.UtcNow;
        RowVersion++;

        RaiseDomainEvent(evt);
    }

    /// <summary>
    /// Atomically updates the sale header and replaces all items in a single mutation.
    /// Produces exactly one RowVersion increment — use this for the PUT endpoint to avoid
    /// the double-increment that would occur when calling Update() + ReplaceItems() separately.
    /// </summary>
    public void UpdateFull(
        Guid customerId, string customerName,
        Guid branchId, string branchName,
        DateTime saleDate,
        IEnumerable<NewSaleItemSpec> newItems)
    {
        if (IsCancelled)
            throw new DomainException("Cannot update a cancelled sale.");

        // Capture previous state before any mutation for the SaleModifiedEvent delta.
        var evt = new SaleModifiedEvent(
            Id, SaleNumber,
            CustomerId, CustomerName, BranchId, BranchName, SaleDate,
            customerId, customerName, branchId, branchName, saleDate);

        // Update header
        CustomerId = customerId;
        CustomerName = customerName;
        BranchId = branchId;
        BranchName = branchName;
        SaleDate = saleDate;

        // Replace items — emit ItemCancelledEvent for each active item removed
        var removed = _items.ToList();
        _items.Clear();

        foreach (var item in removed.Where(i => !i.IsCancelled))
            RaiseDomainEvent(new ItemCancelledEvent(
                Id, item.Id, item.ProductId, item.ProductName,
                item.Quantity, item.UnitPrice, item.TotalAmount));

        foreach (var spec in newItems)
            _items.Add(new SaleItem(Id, spec.ProductId, spec.ProductName, spec.Quantity, spec.UnitPrice));

        RecalculateTotal();
        UpdatedAt = DateTime.UtcNow;
        RowVersion++; // single increment for the entire PUT operation

        foreach (var item in _items)
            RaiseDomainEvent(new ItemAddedEvent(
                Id, item.Id, item.ProductId, item.ProductName,
                item.Quantity, item.UnitPrice, item.Discount.Value, item.TotalAmount));

        RaiseDomainEvent(evt);
    }

    internal IReadOnlyList<SaleItem> ReplaceItems(IEnumerable<NewSaleItemSpec> newItems)
    {
        if (IsCancelled)
            throw new DomainException("Cannot update items of a cancelled sale.");

        // Physically remove all existing items so the repository can delete them as orphans.
        // Raise ItemCancelledEvent for each active item removed — downstream consumers must
        // treat a replace-all PUT identically to individual CancelItem calls for removed items.
        var removed = _items.ToList();
        _items.Clear();

        foreach (var item in removed.Where(i => !i.IsCancelled))
            RaiseDomainEvent(new ItemCancelledEvent(
                Id, item.Id, item.ProductId, item.ProductName,
                item.Quantity, item.UnitPrice, item.TotalAmount));

        foreach (var spec in newItems)
            _items.Add(new SaleItem(Id, spec.ProductId, spec.ProductName, spec.Quantity, spec.UnitPrice));

        RecalculateTotal();
        UpdatedAt = DateTime.UtcNow;
        RowVersion++;

        return removed.AsReadOnly();
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
