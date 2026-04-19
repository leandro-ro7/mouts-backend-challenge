using Ambev.DeveloperEvaluation.Domain.Exceptions;
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

        // Reject immediately if the client's version is stale — avoids a lost update.
        if (sale.RowVersion != command.RowVersion)
            throw new ConcurrencyException(
                $"Sale {command.Id} was modified by another request. " +
                $"Reload the resource and retry. (expected {command.RowVersion}, current {sale.RowVersion})");

        // UpdateFull atomically updates header + replaces items in one RowVersion increment.
        // Raises SaleModifiedEvent and ItemCancelledEvent for each active item removed.
        sale.UpdateFull(
            command.CustomerId, command.CustomerName,
            command.BranchId, command.BranchName, command.SaleDate,
            command.Items.Select(i => new NewSaleItemSpec(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice)));

        // ConcurrencyException may be thrown by the repository if EF Core detects
        // a concurrent modification between the load and the save (DbUpdateConcurrencyException).
        var updated = await _repository.UpdateAsync(sale, cancellationToken);
        return _mapper.Map<UpdateSaleResult>(updated);
    }
}
