using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using AutoMapper;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class UpdateSaleHandlerTests
{
    private readonly ISaleRepository _repository;
    private readonly IMapper _mapper;
    private readonly UpdateSaleHandler _handler;

    public UpdateSaleHandlerTests()
    {
        _repository = Substitute.For<ISaleRepository>();
        _mapper = Substitute.For<IMapper>();
        _handler = new UpdateSaleHandler(_repository, _mapper);
    }

    private static Sale CreateSaleWithItem(int quantity = 5)
    {
        var sale = Sale.Create(Guid.NewGuid(), "Customer", Guid.NewGuid(), "Branch", DateTime.UtcNow, Array.Empty<NewSaleItemSpec>());
        sale.AddItem(Guid.NewGuid(), "Product", quantity, 100m);
        sale.ClearDomainEvents();
        return sale;
    }

    private static UpdateSaleCommand ValidCommand(Guid saleId, uint rowVersion = 0) => new()
    {
        Id = saleId,
        CustomerId = Guid.NewGuid(),
        CustomerName = "Updated Customer",
        BranchId = Guid.NewGuid(),
        BranchName = "Updated Branch",
        SaleDate = DateTime.UtcNow,
        RowVersion = rowVersion,
        Items = new List<UpdateSaleItemDto>
        {
            new() { ProductId = Guid.NewGuid(), ProductName = "New Product", Quantity = 10, UnitPrice = 50m }
        }
    };

    [Fact(DisplayName = "Given existing sale When updating Then SaleModifiedEvent is raised")]
    public async Task Handle_ExistingSale_RaisesSaleModifiedEvent()
    {
        var sale = CreateSaleWithItem();
        var command = ValidCommand(sale.Id);

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        _repository.UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Sale>());
        _mapper.Map<UpdateSaleResult>(Arg.Any<Sale>()).Returns(new UpdateSaleResult());

        await _handler.Handle(command, CancellationToken.None);

        sale.DomainEvents.Should().ContainSingle(e => e is SaleModifiedEvent);
    }

    [Fact(DisplayName = "Given existing sale When updating Then items are replaced with new specs")]
    public async Task Handle_ExistingSale_ReplacesItems()
    {
        var sale = CreateSaleWithItem(quantity: 5);
        var newItems = new List<UpdateSaleItemDto>
        {
            new() { ProductId = Guid.NewGuid(), ProductName = "P1", Quantity = 4, UnitPrice = 10m },
            new() { ProductId = Guid.NewGuid(), ProductName = "P2", Quantity = 15, UnitPrice = 20m }
        };
        var command = ValidCommand(sale.Id);
        command.Items = newItems;

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        _repository.UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Sale>());
        _mapper.Map<UpdateSaleResult>(Arg.Any<Sale>()).Returns(new UpdateSaleResult());

        await _handler.Handle(command, CancellationToken.None);

        // ReplaceItems physically removes old items — only the 2 new items remain
        sale.Items.Count.Should().Be(2);
        sale.Items.Should().NotContain(i => i.ProductName == "Product"); // old item gone
    }

    [Fact(DisplayName = "ReplaceItems directly sets correct discounts on new items")]
    public void ReplaceItems_Domain_SetsCorrectDiscounts()
    {
        var sale = Sale.Create(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow, Array.Empty<NewSaleItemSpec>());
        sale.AddItem(Guid.NewGuid(), "Old", 5, 100m);
        sale.ClearDomainEvents();

        sale.ReplaceItems(new[]
        {
            new NewSaleItemSpec(Guid.NewGuid(), "P1", 4, 10m),
            new NewSaleItemSpec(Guid.NewGuid(), "P2", 15, 20m)
        });

        // ReplaceItems now physically removes old items — only 2 new items remain
        sale.Items.Count.Should().Be(2);
        sale.Items.Should().Contain(i => i.Discount.Value == 0.10m); // P1 qty=4
        sale.Items.Should().Contain(i => i.Discount.Value == 0.20m); // P2 qty=15
    }

    [Fact(DisplayName = "Given existing sale When updating Then repository UpdateAsync is called once")]
    public async Task Handle_ExistingSale_CallsUpdateAsyncOnce()
    {
        var sale = CreateSaleWithItem();
        var command = ValidCommand(sale.Id);

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        _repository.UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Sale>());
        _mapper.Map<UpdateSaleResult>(Arg.Any<Sale>()).Returns(new UpdateSaleResult());

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given non-existent sale ID When updating Then throws KeyNotFoundException")]
    public async Task Handle_SaleNotFound_ThrowsKeyNotFoundException()
    {
        var command = ValidCommand(Guid.NewGuid());
        _repository.GetByIdAsync(command.Id, Arg.Any<CancellationToken>()).Returns((Sale?)null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact(DisplayName = "Given stale RowVersion When updating Then throws ConcurrencyException")]
    public async Task Handle_StaleRowVersion_ThrowsConcurrencyException()
    {
        var sale = CreateSaleWithItem(); // RowVersion = 0 on a fresh sale
        var command = ValidCommand(sale.Id, rowVersion: 99); // client claims version 99

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given matching RowVersion When updating Then proceeds normally")]
    public async Task Handle_MatchingRowVersion_Succeeds()
    {
        var sale = CreateSaleWithItem(); // RowVersion = 0
        var command = ValidCommand(sale.Id, rowVersion: 0); // matches

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        _repository.UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Sale>());
        _mapper.Map<UpdateSaleResult>(Arg.Any<Sale>()).Returns(new UpdateSaleResult());

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().NotThrowAsync();
        await _repository.Received(1).UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "UpdateFull raises ItemCancelledEvent for each active item removed")]
    public async Task Handle_ExistingSale_RaisesItemCancelledEventForRemovedActiveItems()
    {
        var sale = Sale.Create(Guid.NewGuid(), "Customer", Guid.NewGuid(), "Branch", DateTime.UtcNow, Array.Empty<NewSaleItemSpec>());
        sale.AddItem(Guid.NewGuid(), "OldProduct1", 2, 100m);
        sale.AddItem(Guid.NewGuid(), "OldProduct2", 3, 50m);
        sale.ClearDomainEvents();

        var command = ValidCommand(sale.Id);

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        _repository.UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Sale>());
        _mapper.Map<UpdateSaleResult>(Arg.Any<Sale>()).Returns(new UpdateSaleResult());

        await _handler.Handle(command, CancellationToken.None);

        sale.DomainEvents.OfType<ItemCancelledEvent>()
            .Should().HaveCount(2, "both original active items were replaced");
    }

    [Fact(DisplayName = "Single PUT increments RowVersion exactly once")]
    public async Task Handle_SinglePut_IncrementsRowVersionByExactlyOne()
    {
        var sale = CreateSaleWithItem();
        var initialVersion = sale.RowVersion; // 0
        var command = ValidCommand(sale.Id, rowVersion: initialVersion);

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        _repository.UpdateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Sale>());
        _mapper.Map<UpdateSaleResult>(Arg.Any<Sale>()).Returns(new UpdateSaleResult());

        await _handler.Handle(command, CancellationToken.None);

        sale.RowVersion.Should().Be(initialVersion + 1,
            "UpdateFull is one logical mutation — exactly one RowVersion increment");
    }
}
