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

    [Fact(DisplayName = "Given items on Create Then TotalAmount is calculated with discount")]
    public void Create_WithItems_RecalculatesTotalAmount()
    {
        // qty=4 → 0% discount (rule: "above 4") → total = 4 * 10 = 40
        var sale = SaleTestData.CreateSaleWithItem(4, 10m);
        sale.TotalAmount.Should().Be(40m);
    }

    [Fact(DisplayName = "Given qty=10 on Create Then 20% discount applied")]
    public void Create_Qty10_Applies20PercentDiscount()
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

    [Fact(DisplayName = "Given cancelled sale When UpdateFull Then throws DomainException")]
    public void UpdateFull_OnCancelledSale_ThrowsDomainException()
    {
        var sale = SaleTestData.CreateValidSale();
        sale.Cancel();

        var act = () => sale.UpdateFull(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow,
            new[] { new NewSaleItemSpec(Guid.NewGuid(), "Product", 1, 10m) });
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

    [Fact(DisplayName = "When UpdateFull Then SaleModifiedEvent is raised")]
    public void UpdateFull_RaisesSaleModifiedEvent()
    {
        var sale = SaleTestData.CreateValidSale();
        sale.ClearDomainEvents();

        sale.UpdateFull(Guid.NewGuid(), "New Customer", Guid.NewGuid(), "New Branch", DateTime.UtcNow,
            new[] { new NewSaleItemSpec(Guid.NewGuid(), "P", 2, 10m) });

        sale.DomainEvents.Should().Contain(e => e is SaleModifiedEvent);
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

    [Fact(DisplayName = "UpdateFull with two active items raises two ItemCancelledEvents")]
    public void UpdateFull_TwoActiveItems_RaisesTwoItemCancelledEvents()
    {
        var sale = DomainSale.Create(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow,
            new[] { new NewSaleItemSpec(Guid.NewGuid(), "Item1", 2, 10m), new NewSaleItemSpec(Guid.NewGuid(), "Item2", 3, 20m) });
        sale.ClearDomainEvents();

        sale.UpdateFull(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow,
            new[] { new NewSaleItemSpec(Guid.NewGuid(), "NewItem", 5, 15m) });

        sale.DomainEvents.OfType<ItemCancelledEvent>()
            .Should().HaveCount(2, "one event per active item removed");
    }

    [Fact(DisplayName = "UpdateFull skips already-cancelled items when raising ItemCancelledEvent")]
    public void UpdateFull_AlreadyCancelledItem_NotRaisedAgain()
    {
        var sale = DomainSale.Create(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow,
            new[] { new NewSaleItemSpec(Guid.NewGuid(), "Active", 2, 10m), new NewSaleItemSpec(Guid.NewGuid(), "ToCancel", 2, 10m) });
        sale.CancelItem(sale.Items[1].Id);
        sale.ClearDomainEvents();

        sale.UpdateFull(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow,
            new[] { new NewSaleItemSpec(Guid.NewGuid(), "NewItem", 5, 15m) });

        sale.DomainEvents.OfType<ItemCancelledEvent>()
            .Should().HaveCount(1, "only the active item raises ItemCancelledEvent");
    }

    [Fact(DisplayName = "UpdateFull with no active items raises no ItemCancelledEvents")]
    public void UpdateFull_NoActiveItems_RaisesNoItemCancelledEvent()
    {
        var sale = DomainSale.Create(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow,
            new[] { new NewSaleItemSpec(Guid.NewGuid(), "OnlyItem", 2, 10m) });
        sale.CancelItem(sale.Items[0].Id);
        sale.ClearDomainEvents();

        sale.UpdateFull(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow,
            new[] { new NewSaleItemSpec(Guid.NewGuid(), "NewItem", 5, 15m) });

        sale.DomainEvents.OfType<ItemCancelledEvent>()
            .Should().BeEmpty("all existing items were already cancelled");
    }

    [Fact(DisplayName = "UpdateFull increments RowVersion exactly once for a header+items mutation")]
    public void UpdateFull_IncrementsRowVersionExactlyOnce()
    {
        var sale = SaleTestData.CreateValidSale();
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
        var sale = SaleTestData.CreateSaleWithItem(3, 10m);
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

    [Fact(DisplayName = "SaleModifiedEvent carries correct Previous.TotalAmount and Current.TotalAmount")]
    public void UpdateFull_SaleModifiedEvent_CarriesFinancialDelta()
    {
        // Old item: qty=3, price=10 → no discount → total = 30
        var sale = SaleTestData.CreateSaleWithItem(3, 10m);
        var previousTotal = sale.TotalAmount; // 30
        sale.ClearDomainEvents();

        // New item: qty=5, price=20 → 10% discount → total = 5 * 20 * 0.90 = 90
        sale.UpdateFull(
            sale.CustomerId, sale.CustomerName,
            sale.BranchId, sale.BranchName,
            sale.SaleDate,
            new[] { new NewSaleItemSpec(Guid.NewGuid(), "NewItem", 5, 20m) });

        var evt = sale.DomainEvents.OfType<SaleModifiedEvent>().Single();
        evt.Previous.TotalAmount.Should().Be(previousTotal, "captures total before mutation");
        evt.Current.TotalAmount.Should().Be(sale.TotalAmount, "captures total after recalculation");
        evt.Current.TotalAmount.Should().NotBe(previousTotal, "the amounts must differ when items change");
    }

    [Fact(DisplayName = "UpdateFull with same items preserves TotalAmount in both snapshot fields")]
    public void UpdateFull_SameItems_SaleModifiedEvent_TotalAmountUnchanged()
    {
        var prodId = Guid.NewGuid();
        var sale = DomainSale.Create(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow,
            new[] { new NewSaleItemSpec(prodId, "Item", 5, 10m) });
        var totalBefore = sale.TotalAmount;
        sale.ClearDomainEvents();

        // Replace with the exact same item spec — total should not change
        sale.UpdateFull(Guid.NewGuid(), "New Customer", Guid.NewGuid(), "New Branch", DateTime.UtcNow,
            new[] { new NewSaleItemSpec(prodId, "Item", 5, 10m) });

        var evt = sale.DomainEvents.OfType<SaleModifiedEvent>().Single();
        evt.Previous.TotalAmount.Should().Be(totalBefore);
        evt.Current.TotalAmount.Should().Be(totalBefore, "same items means same total");
    }
}
