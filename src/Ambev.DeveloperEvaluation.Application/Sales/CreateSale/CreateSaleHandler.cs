using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using AutoMapper;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.CreateSale;

public class CreateSaleHandler : IRequestHandler<CreateSaleCommand, CreateSaleResult>
{
    private readonly ISaleRepository _repository;
    private readonly IMapper _mapper;

    public CreateSaleHandler(ISaleRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<CreateSaleResult> Handle(CreateSaleCommand command, CancellationToken cancellationToken)
    {
        // Items are passed to Create so the SaleCreatedEvent snapshot is complete at emission time.
        var sale = Sale.Create(
            command.CustomerId, command.CustomerName,
            command.BranchId, command.BranchName,
            command.SaleDate,
            command.Items.Select(i => new NewSaleItemSpec(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice)));

        var created = await _repository.CreateAsync(sale, cancellationToken);

        return _mapper.Map<CreateSaleResult>(created);
    }
}
