using Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
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
        var sale = Sale.Create(Guid.NewGuid(), "Customer", Guid.NewGuid(), "Branch", DateTime.UtcNow);
        var item1 = sale.AddItem(Guid.NewGuid(), "Product A", 5, 100m);
        var item2 = sale.AddItem(Guid.NewGuid(), "Product B", 2, 50m);
        sale.ClearDomainEvents();

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        _repository.UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>()).Returns(sale);

        var result = await _handler.Handle(
            new CancelSaleItemCommand { SaleId = sale.Id, ItemId = item1.Id }, CancellationToken.None);

        result.NewSaleTotalAmount.Should().Be(100m);
        sale.DomainEvents.Should().ContainSingle(e => e is ItemCancelledEvent);
    }

    [Fact(DisplayName = "Given cancelled sale When cancelling item Then throws DomainException")]
    public async Task Handle_CancelledSale_ThrowsDomainException()
    {
        var sale = Sale.Create(Guid.NewGuid(), "Customer", Guid.NewGuid(), "Branch", DateTime.UtcNow);
        var item = sale.AddItem(Guid.NewGuid(), "Product", 2, 50m);
        sale.Cancel();

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);

        var act = () => _handler.Handle(
            new CancelSaleItemCommand { SaleId = sale.Id, ItemId = item.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*cancelled sale*");
    }

    [Fact(DisplayName = "Given already-cancelled item When cancelling again Then throws DomainException")]
    public async Task Handle_AlreadyCancelledItem_ThrowsDomainException()
    {
        var sale = Sale.Create(Guid.NewGuid(), "Customer", Guid.NewGuid(), "Branch", DateTime.UtcNow);
        var item = sale.AddItem(Guid.NewGuid(), "Product", 2, 50m);
        sale.CancelItem(item.Id);

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);

        var act = () => _handler.Handle(
            new CancelSaleItemCommand { SaleId = sale.Id, ItemId = item.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*already cancelled*");
    }
}
