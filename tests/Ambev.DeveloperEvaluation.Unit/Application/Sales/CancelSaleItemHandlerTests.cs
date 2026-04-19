using Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class CancelSaleItemHandlerTests
{
    private readonly ISaleRepository _repository;
    private readonly CancelSaleItemHandler _handler;

    public CancelSaleItemHandlerTests()
    {
        _repository = Substitute.For<ISaleRepository>();
        _handler = new CancelSaleItemHandler(_repository);
    }

    [Fact(DisplayName = "Given active sale with item When cancelling item Then recalculates total and raises ItemCancelledEvent")]
    public async Task Handle_ActiveItem_CancelsItemRecalculatesTotalAndRaisesEvent()
    {
        // item1: qty=5, price=100 → 10% discount → total = 450
        // item2: qty=2, price=50  → 0% discount  → total = 100
        var sale = Sale.Create(Guid.NewGuid(), "Customer", Guid.NewGuid(), "Branch", DateTime.UtcNow,
            new[]
            {
                new NewSaleItemSpec(Guid.NewGuid(), "Product A", 5, 100m),
                new NewSaleItemSpec(Guid.NewGuid(), "Product B", 2, 50m)
            });
        var item1Id = sale.Items[0].Id;
        sale.ClearDomainEvents();

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        _repository.UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>()).Returns(sale);

        var result = await _handler.Handle(
            new CancelSaleItemCommand { SaleId = sale.Id, ItemId = item1Id }, CancellationToken.None);

        result.NewSaleTotalAmount.Should().Be(100m);
        sale.DomainEvents.Should().ContainSingle(e => e is ItemCancelledEvent);
    }

    [Fact(DisplayName = "Given cancelled sale When cancelling item Then throws DomainException")]
    public async Task Handle_CancelledSale_ThrowsDomainException()
    {
        var sale = Sale.Create(Guid.NewGuid(), "Customer", Guid.NewGuid(), "Branch", DateTime.UtcNow,
            new[] { new NewSaleItemSpec(Guid.NewGuid(), "Product", 2, 50m) });
        var itemId = sale.Items[0].Id;
        sale.Cancel();

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);

        var act = () => _handler.Handle(
            new CancelSaleItemCommand { SaleId = sale.Id, ItemId = itemId }, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*cancelled sale*");
    }

    [Fact(DisplayName = "Given already-cancelled item When cancelling again Then throws DomainException")]
    public async Task Handle_AlreadyCancelledItem_ThrowsDomainException()
    {
        var sale = Sale.Create(Guid.NewGuid(), "Customer", Guid.NewGuid(), "Branch", DateTime.UtcNow,
            new[] { new NewSaleItemSpec(Guid.NewGuid(), "Product", 2, 50m) });
        var itemId = sale.Items[0].Id;
        sale.CancelItem(itemId);

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);

        var act = () => _handler.Handle(
            new CancelSaleItemCommand { SaleId = sale.Id, ItemId = itemId }, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*already cancelled*");
    }
}
