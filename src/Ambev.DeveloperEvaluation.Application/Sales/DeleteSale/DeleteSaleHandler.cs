using Ambev.DeveloperEvaluation.Domain.Repositories;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.DeleteSale;

/// <summary>
/// DELETE performs a soft-delete via cancellation.
/// Sale records are immutable historical/financial data and must not be physically removed.
/// </summary>
public class DeleteSaleHandler : IRequestHandler<DeleteSaleCommand, DeleteSaleResult>
{
    private readonly ISaleRepository _repository;

    public DeleteSaleHandler(ISaleRepository repository)
    {
        _repository = repository;
    }

    public async Task<DeleteSaleResult> Handle(DeleteSaleCommand command, CancellationToken cancellationToken)
    {
        var sale = await _repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Sale with ID {command.Id} was not found.");

        if (!sale.IsCancelled)
        {
            sale.Cancel(); // raises SaleCancelledEvent internally
            await _repository.UpdateAsync(sale, cancellationToken);
        }

        return new DeleteSaleResult { Success = true };
    }
}
