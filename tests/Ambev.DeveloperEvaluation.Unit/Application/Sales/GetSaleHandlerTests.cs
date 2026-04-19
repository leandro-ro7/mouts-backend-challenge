using Ambev.DeveloperEvaluation.Application.Sales.GetSale;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using AutoMapper;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class GetSaleHandlerTests
{
    private readonly ISaleRepository _repository = Substitute.For<ISaleRepository>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();

    private GetSaleHandler Handler() => new(_repository, _mapper);

    [Fact(DisplayName = "Given existing sale ID When getting Then returns mapped result")]
    public async Task Handle_ExistingId_ReturnsMappedResult()
    {
        var sale = Sale.Create(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow, Array.Empty<NewSaleItemSpec>());
        var expected = new GetSaleResult { Id = sale.Id };

        _repository.GetByIdAsync(sale.Id, Arg.Any<CancellationToken>()).Returns(sale);
        _mapper.Map<GetSaleResult>(sale).Returns(expected);

        var result = await Handler().Handle(new GetSaleQuery { Id = sale.Id }, CancellationToken.None);

        result.Should().Be(expected);
        await _repository.Received(1).GetByIdAsync(sale.Id, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given unknown sale ID When getting Then throws KeyNotFoundException")]
    public async Task Handle_UnknownId_ThrowsKeyNotFoundException()
    {
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Sale?)null);

        var act = async () => await Handler().Handle(new GetSaleQuery { Id = id }, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage($"*{id}*");
    }
}
