using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using AutoMapper;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;

public class UpdateSaleHandler : IRequestHandler<UpdateSaleCommand, UpdateSaleResult>
{
    private readonly ISaleRepository _repository;
    private readonly IMapper _mapper;

    public UpdateSaleHandler(ISaleRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<UpdateSaleResult> Handle(UpdateSaleCommand command, CancellationToken cancellationToken)
    {
        var sale = await _repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Sale with ID {command.Id} was not found.");

        // Update raises SaleModifiedEvent internally
        sale.Update(command.CustomerId, command.CustomerName,
            command.BranchId, command.BranchName, command.SaleDate);

        // ReplaceItems replaces all items WITHOUT raising ItemCancelledEvent for each —
        // this is a replace-all operation, not individual business cancellations
        sale.ReplaceItems(command.Items
            .Select(i => new NewSaleItemSpec(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice)));

        var updated = await _repository.UpdateAsync(sale, cancellationToken);

        return _mapper.Map<UpdateSaleResult>(updated);
    }
}
