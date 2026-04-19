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

    // Client-supplied idempotency key — if provided, a second Create with the same key
    // returns the original sale instead of creating a duplicate. Optional; null means no dedup.
    public Guid? IdempotencyKey { get; private set; }

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
        IEnumerable<NewSaleItemSpec> items,
        Guid? idempotencyKey = null)
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
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTime.UtcNow
        };

        // Add items before raising the event so the snapshot is complete.
        foreach (var spec in items)
            sale._items.Add(new SaleItem(sale.Id, spec.ProductId, spec.ProductName, spec.Quantity, spec.UnitPrice));

        sale.RecalculateTotal();

        sale.RaiseDomainEvent(new SaleCreatedEvent(
            sale.Id, sale.SaleNumber,
            customerId, customerName,
            branchId, branchName,
            saleDate, sale.TotalAmount, sale.BuildSnapshot().Items));

        return sale;
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
        BumpVersion();

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
        BumpVersion();

        RaiseDomainEvent(new SaleCancelledEvent(
            Id, SaleNumber,
            CustomerId, CustomerName,
            BranchId, BranchName,
            TotalAmount));
    }

    /// <summary>
    /// Atomically updates the sale header and replaces all items in a single mutation.
    /// Produces exactly one RowVersion increment — use this for the PUT endpoint.
    /// </summary>
    public void UpdateFull(
        Guid customerId, string customerName,
        Guid branchId, string branchName,
        DateTime saleDate,
        IEnumerable<NewSaleItemSpec> newItems)
    {
        if (IsCancelled)
            throw new DomainException("Cannot update a cancelled sale.");

        // Capture previous snapshot before any mutation so SaleModifiedEvent.Previous is accurate.
        var previous = BuildSnapshot();

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
        BumpVersion(); // single increment for the entire PUT operation

        // SaleModifiedEvent carries full before/after snapshots (items included) so downstream
        // consumers can rebuild state without querying the database.
        RaiseDomainEvent(new SaleModifiedEvent(Id, SaleNumber, previous, BuildSnapshot()));
    }

    private void BumpVersion()
    {
        RowVersion++;
        UpdatedAt = DateTime.UtcNow;
    }

    private SaleSnapshot BuildSnapshot() =>
        new(CustomerId, CustomerName, BranchId, BranchName, SaleDate, TotalAmount,
            _items.Select(i => new SaleItemSnapshot(
                i.Id, i.ProductId, i.ProductName, i.Quantity, i.UnitPrice,
                i.Discount.Value, i.TotalAmount))
            .ToList()
            .AsReadOnly());

    private void RecalculateTotal()
    {
        TotalAmount = _items
            .Where(i => !i.IsCancelled)
            .Sum(i => i.TotalAmount);
    }

    private static string GenerateSaleNumber() =>
        $"{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
}
