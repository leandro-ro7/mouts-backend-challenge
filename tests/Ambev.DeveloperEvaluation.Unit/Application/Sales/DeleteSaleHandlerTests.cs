using Ambev.DeveloperEvaluation.Application.Sales.DeleteSale;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class DeleteSaleHandlerTests
{
    private readonly ISaleRepository _repository = Substitute.For<ISaleRepository>();

    private DeleteSaleHandler Handler() => new(_repository);

    [Fact(DisplayName = "Given active sale When deleting Then sale is soft-cancelled and persisted")]
    public async Task Handle_ActiveSale_CancelsSaleAndPersists()
    {
        var sale = Sale.Create(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow, Array.Empty<NewSaleItemSpec>());
        sale.ClearDomainEvents();

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        _repository.UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Sale>());

        var result = await Handler().Handle(new DeleteSaleCommand { Id = sale.Id }, CancellationToken.None);

        result.Success.Should().BeTrue();
        sale.IsCancelled.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(sale, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given already-cancelled sale When deleting Then skips cancel and returns success")]
    public async Task Handle_AlreadyCancelledSale_SkipsCancelAndSucceeds()
    {
        var sale = Sale.Create(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow, Array.Empty<NewSaleItemSpec>());
        sale.Cancel();
        sale.ClearDomainEvents();

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);

        var result = await Handler().Handle(new DeleteSaleCommand { Id = sale.Id }, CancellationToken.None);

        result.Success.Should().BeTrue();
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given unknown sale ID When deleting Then throws KeyNotFoundException")]
    public async Task Handle_UnknownId_ThrowsKeyNotFoundException()
    {
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Sale?)null);

        var act = async () => await Handler().Handle(new DeleteSaleCommand { Id = id }, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage($"*{id}*");
    }
}
