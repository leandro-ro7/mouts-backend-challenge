using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using Ambev.DeveloperEvaluation.Unit.Domain.Entities.Sale.TestData;
using FluentAssertions;
using Xunit;

using DomainSale = Ambev.DeveloperEvaluation.Domain.Entities.Sale;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Entities.Sale;

public class SaleTests
{
    [Fact(DisplayName = "Given valid data When Sale.Create Then SaleNumber is generated server-side")]
    public void Create_GeneratesSaleNumber_ServerSide()
    {
        var sale = SaleTestData.CreateValidSale();
        sale.SaleNumber.Should().NotBeNullOrEmpty();
        sale.SaleNumber.Should().MatchRegex(@"^\d{8}-[A-F0-9]{8}$");
    }

    [Fact(DisplayName = "Given items When adding Then TotalAmount is recalculated")]
    public void AddItem_RecalculatesTotalAmount()
    {
        // qty=4 → 10% discount → total = 4 * 10 * 0.90 = 36
        var sale = SaleTestData.CreateSaleWithItem(4, 10m);
        sale.TotalAmount.Should().Be(36m);
    }

    [Fact(DisplayName = "Given item qty=10 When adding Then 20% discount applied")]
    public void AddItem_Qty10_Applies20PercentDiscount()
    {
        // qty=10 → 20% discount → total = 10 * 10 * 0.80 = 80
        var sale = SaleTestData.CreateSaleWithItem(10, 10m);
        sale.Items[0].Discount.Value.Should().Be(0.20m);
        sale.TotalAmount.Should().Be(80m);
    }

    [Fact(DisplayName = "Given cancelled item When recalculating Then item excluded from total")]
    public void CancelItem_ExcludesItemFromTotal()
    {
        var sale = SaleTestData.CreateValidSale();
        var itemId = sale.Items[0].Id;
        var totalBefore = sale.TotalAmount;

        sale.CancelItem(itemId);

        sale.TotalAmount.Should().Be(0m);
        sale.Items[0].IsCancelled.Should().BeTrue();
    }

    [Fact(DisplayName = "Given active sale When Cancel Then IsCancelled is true")]
    public void Cancel_SetsCancelledFlag()
    {
        var sale = SaleTestData.CreateValidSale();
        sale.Cancel();
        sale.IsCancelled.Should().BeTrue();
    }

    [Fact(DisplayName = "Given already cancelled sale When Cancel Then throws DomainException")]
    public void Cancel_WhenAlreadyCancelled_ThrowsDomainException()
    {
        var sale = SaleTestData.CreateValidSale();
        sale.Cancel();

        var act = () => sale.Cancel();
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "Given cancelled sale When AddItem Then throws DomainException")]
    public void AddItem_ToACancelledSale_ThrowsDomainException()
    {
        var sale = SaleTestData.CreateValidSale();
        sale.Cancel();

        var act = () => sale.AddItem(Guid.NewGuid(), "Product", 1, 10m);
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "Given cancelled sale When CancelItem Then throws DomainException")]
    public void CancelItem_OnCancelledSale_ThrowsDomainException()
    {
        var sale = SaleTestData.CreateValidSale();
        var itemId = sale.Items[0].Id;
        sale.Cancel();

        var act = () => sale.CancelItem(itemId);
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "Given non-existent itemId When CancelItem Then throws DomainException")]
    public void CancelItem_NonExistentItem_ThrowsDomainException()
    {
        var sale = SaleTestData.CreateValidSale();

        var act = () => sale.CancelItem(Guid.NewGuid());
        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "When Sale.Create Then SaleCreatedEvent is raised")]
    public void Create_RaisesSaleCreatedEvent()
    {
        var sale = SaleTestData.CreateValidSale();
        sale.DomainEvents.Should().ContainSingle(e => e is SaleCreatedEvent);
    }

    [Fact(DisplayName = "When Cancel Then SaleCancelledEvent is raised")]
    public void Cancel_RaisesSaleCancelledEvent()
    {
        var sale = SaleTestData.CreateValidSale();
        sale.ClearDomainEvents();

        sale.Cancel();

        sale.DomainEvents.Should().ContainSingle(e => e is SaleCancelledEvent);
    }

    [Fact(DisplayName = "When Update Then SaleModifiedEvent is raised")]
    public void Update_RaisesSaleModifiedEvent()
    {
        var sale = SaleTestData.CreateValidSale();
        sale.ClearDomainEvents();

        sale.Update(Guid.NewGuid(), "New Customer", Guid.NewGuid(), "New Branch", DateTime.UtcNow);

        sale.DomainEvents.Should().ContainSingle(e => e is SaleModifiedEvent);
    }

    [Fact(DisplayName = "When CancelItem Then ItemCancelledEvent is raised")]
    public void CancelItem_RaisesItemCancelledEvent()
    {
        var sale = SaleTestData.CreateValidSale();
        var itemId = sale.Items[0].Id;
        sale.ClearDomainEvents();

        sale.CancelItem(itemId);

        sale.DomainEvents.Should().ContainSingle(e => e is ItemCancelledEvent);
    }

    [Fact(DisplayName = "ReplaceItems with two active items raises two ItemCancelledEvents")]
    public void ReplaceItems_TwoActiveItems_RaisesTwoItemCancelledEvents()
    {
        var sale = DomainSale.Create(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow, Array.Empty<NewSaleItemSpec>());
        sale.AddItem(Guid.NewGuid(), "Item1", 2, 10m);
        sale.AddItem(Guid.NewGuid(), "Item2", 3, 20m);
        sale.ClearDomainEvents();

        sale.ReplaceItems(new[]
        {
            new NewSaleItemSpec(Guid.NewGuid(), "NewItem", 5, 15m)
        });

        sale.DomainEvents.OfType<ItemCancelledEvent>()
            .Should().HaveCount(2, "one event per active item removed");
    }

    [Fact(DisplayName = "ReplaceItems skips already-cancelled items when raising ItemCancelledEvent")]
    public void ReplaceItems_AlreadyCancelledItem_NotRaisedAgain()
    {
        var sale = DomainSale.Create(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow, Array.Empty<NewSaleItemSpec>());
        sale.AddItem(Guid.NewGuid(), "Active", 2, 10m);
        var cancelledItemId = sale.AddItem(Guid.NewGuid(), "Cancelled", 2, 10m).Id;
        sale.CancelItem(cancelledItemId);
        sale.ClearDomainEvents();

        sale.ReplaceItems(new[]
        {
            new NewSaleItemSpec(Guid.NewGuid(), "NewItem", 5, 15m)
        });

        // Only the active item should raise ItemCancelledEvent; the already-cancelled one must not
        sale.DomainEvents.OfType<ItemCancelledEvent>()
            .Should().HaveCount(1, "only the active item raises ItemCancelledEvent");
    }

    [Fact(DisplayName = "ReplaceItems with no active items raises no ItemCancelledEvents")]
    public void ReplaceItems_NoActiveItems_RaisesNoItemCancelledEvent()
    {
        var sale = DomainSale.Create(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow, Array.Empty<NewSaleItemSpec>());
        var itemId = sale.AddItem(Guid.NewGuid(), "OnlyItem", 2, 10m).Id;
        sale.CancelItem(itemId);
        sale.ClearDomainEvents();

        sale.ReplaceItems(new[]
        {
            new NewSaleItemSpec(Guid.NewGuid(), "NewItem", 5, 15m)
        });

        sale.DomainEvents.OfType<ItemCancelledEvent>()
            .Should().BeEmpty("all existing items were already cancelled");
    }

    [Fact(DisplayName = "UpdateFull increments RowVersion exactly once for a header+items mutation")]
    public void UpdateFull_IncrementsRowVersionExactlyOnce()
    {
        var sale = DomainSale.Create(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow, Array.Empty<NewSaleItemSpec>());
        sale.AddItem(Guid.NewGuid(), "Old", 2, 10m);
        sale.ClearDomainEvents();
        var initialVersion = sale.RowVersion;

        sale.UpdateFull(
            Guid.NewGuid(), "New Customer",
            Guid.NewGuid(), "New Branch",
            DateTime.UtcNow,
            new[] { new NewSaleItemSpec(Guid.NewGuid(), "NewItem", 5, 20m) });

        sale.RowVersion.Should().Be(initialVersion + 1,
            "a single PUT is one logical mutation — one RowVersion increment");
    }

    [Fact(DisplayName = "UpdateFull raises both SaleModifiedEvent and ItemCancelledEvent")]
    public void UpdateFull_RaisesSaleModifiedAndItemCancelledEvents()
    {
        var sale = DomainSale.Create(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow, Array.Empty<NewSaleItemSpec>());
        sale.AddItem(Guid.NewGuid(), "OldItem", 3, 10m);
        sale.ClearDomainEvents();

        sale.UpdateFull(
            Guid.NewGuid(), "New Customer",
            Guid.NewGuid(), "New Branch",
            DateTime.UtcNow,
            new[] { new NewSaleItemSpec(Guid.NewGuid(), "NewItem", 5, 20m) });

        sale.DomainEvents.Should().ContainSingle(e => e is SaleModifiedEvent);
        sale.DomainEvents.Should().ContainSingle(e => e is ItemCancelledEvent,
            "the one active old item must raise ItemCancelledEvent");
    }
}
