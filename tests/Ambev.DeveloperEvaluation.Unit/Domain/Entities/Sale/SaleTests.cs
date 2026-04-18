using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Unit.Domain.Entities.Sale.TestData;
using FluentAssertions;
using Xunit;

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
        sale.Items[0].Discount.Should().Be(0.20m);
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
}
