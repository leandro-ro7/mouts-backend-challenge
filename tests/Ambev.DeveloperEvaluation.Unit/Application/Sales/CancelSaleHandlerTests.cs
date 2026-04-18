using Ambev.DeveloperEvaluation.Application.Sales.CancelSale;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class CancelSaleHandlerTests
{
    private readonly ISaleRepository _repository;
    private readonly CancelSaleHandler _handler;

    public CancelSaleHandlerTests()
    {
        _repository = Substitute.For<ISaleRepository>();
        _handler = new CancelSaleHandler(_repository);
    }

    [Fact(DisplayName = "Given existing active sale When cancelling Then marks cancelled and raises SaleCancelledEvent")]
    public async Task Handle_ActiveSale_CancelsSaleAndRaisesEvent()
    {
        var sale = Sale.Create(Guid.NewGuid(), "Customer", Guid.NewGuid(), "Branch", DateTime.UtcNow);
        sale.AddItem(Guid.NewGuid(), "Product", 2, 50m);
        sale.ClearDomainEvents(); // clear Create event for clean assertion

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        _repository.UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>()).Returns(sale);

        var result = await _handler.Handle(new CancelSaleCommand { Id = sale.Id }, CancellationToken.None);

        result.IsCancelled.Should().BeTrue();
        sale.DomainEvents.Should().ContainSingle(e => e is SaleCancelledEvent);
        await _repository.Received(1).UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given already-cancelled sale When cancelling again Then throws DomainException")]
    public async Task Handle_AlreadyCancelledSale_ThrowsDomainException()
    {
        var sale = Sale.Create(Guid.NewGuid(), "Customer", Guid.NewGuid(), "Branch", DateTime.UtcNow);
        sale.Cancel();

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);

        var act = () => _handler.Handle(new CancelSaleCommand { Id = sale.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*already cancelled*");
    }

    [Fact(DisplayName = "Given non-existent sale ID When cancelling Then throws KeyNotFoundException")]
    public async Task Handle_NonExistentSale_ThrowsKeyNotFoundException()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Sale?)null);

        var act = () => _handler.Handle(new CancelSaleCommand { Id = Guid.NewGuid() }, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
