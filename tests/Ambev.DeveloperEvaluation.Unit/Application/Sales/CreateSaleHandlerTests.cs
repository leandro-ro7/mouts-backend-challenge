using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Unit.Application.Sales.TestData;
using AutoMapper;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class CreateSaleHandlerTests
{
    private readonly ISaleRepository _repository;
    private readonly IMapper _mapper;
    private readonly CreateSaleHandler _handler;

    public CreateSaleHandlerTests()
    {
        _repository = Substitute.For<ISaleRepository>();
        _mapper = Substitute.For<IMapper>();
        _handler = new CreateSaleHandler(_repository, _mapper);
    }

    [Fact(DisplayName = "Given valid command When handling Then creates sale and persists it")]
    public async Task Handle_ValidCommand_CreatesSaleAndPersists()
    {
        var command = CreateSaleHandlerTestData.ValidCommand(itemQuantity: 4);

        _repository.CreateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Sale>());
        _mapper.Map<CreateSaleResult>(Arg.Any<Sale>())
            .Returns(new CreateSaleResult { Id = Guid.NewGuid() });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        await _repository.Received(1).CreateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given item with qty=4 When creating Then 10% discount is applied")]
    public async Task Handle_ItemQty4_Applies10PercentDiscount()
    {
        var command = CreateSaleHandlerTestData.ValidCommand(itemQuantity: 4);
        Sale? capturedSale = null;

        _repository.CreateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => { capturedSale = callInfo.Arg<Sale>(); return capturedSale; });
        _mapper.Map<CreateSaleResult>(Arg.Any<Sale>()).Returns(new CreateSaleResult());

        await _handler.Handle(command, CancellationToken.None);

        capturedSale!.Items[0].Discount.Value.Should().Be(0.10m);
    }

    [Fact(DisplayName = "Given item with qty=10 When creating Then 20% discount is applied")]
    public async Task Handle_ItemQty10_Applies20PercentDiscount()
    {
        var command = CreateSaleHandlerTestData.ValidCommand(itemQuantity: 10);
        Sale? capturedSale = null;

        _repository.CreateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => { capturedSale = callInfo.Arg<Sale>(); return capturedSale; });
        _mapper.Map<CreateSaleResult>(Arg.Any<Sale>()).Returns(new CreateSaleResult());

        await _handler.Handle(command, CancellationToken.None);

        capturedSale!.Items[0].Discount.Value.Should().Be(0.20m);
    }

    [Fact(DisplayName = "Given IdempotencyKey already used When creating Then returns existing sale without re-creating")]
    public async Task Handle_DuplicateIdempotencyKey_ReturnsExistingSaleWithoutCreate()
    {
        var key = Guid.NewGuid();
        var command = CreateSaleHandlerTestData.ValidCommand(itemQuantity: 4);
        command.IdempotencyKey = key;

        var existingSale = Sale.Create(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow,
            Array.Empty<Ambev.DeveloperEvaluation.Domain.ValueObjects.NewSaleItemSpec>(), key);
        var expectedResult = new CreateSaleResult { Id = existingSale.Id };

        _repository.GetByIdempotencyKeyAsync(key, Arg.Any<CancellationToken>()).Returns(existingSale);
        _mapper.Map<CreateSaleResult>(existingSale).Returns(expectedResult);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Id.Should().Be(existingSale.Id);
        await _repository.DidNotReceive().CreateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given new IdempotencyKey When creating Then persists sale with key stored")]
    public async Task Handle_NewIdempotencyKey_PersistsSaleWithKey()
    {
        var key = Guid.NewGuid();
        var command = CreateSaleHandlerTestData.ValidCommand(itemQuantity: 4);
        command.IdempotencyKey = key;

        _repository.GetByIdempotencyKeyAsync(key, Arg.Any<CancellationToken>()).Returns((Sale?)null);
        Sale? captured = null;
        _repository.CreateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => { captured = callInfo.Arg<Sale>(); return captured!; });
        _mapper.Map<CreateSaleResult>(Arg.Any<Sale>()).Returns(new CreateSaleResult());

        await _handler.Handle(command, CancellationToken.None);

        captured!.IdempotencyKey.Should().Be(key);
        await _repository.Received(1).CreateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
    }
}
