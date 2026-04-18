using Ambev.DeveloperEvaluation.Domain.Repositories;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSale;

public class CancelSaleHandler : IRequestHandler<CancelSaleCommand, CancelSaleResult>
{
    private readonly ISaleRepository _repository;

    public CancelSaleHandler(ISaleRepository repository)
    {
        _repository = repository;
    }

    public async Task<CancelSaleResult> Handle(CancelSaleCommand command, CancellationToken cancellationToken)
    {
        var sale = await _repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Sale with ID {command.Id} was not found.");

        sale.Cancel(); // raises SaleCancelledEvent internally

        await _repository.UpdateAsync(sale, cancellationToken);

        return new CancelSaleResult
        {
            Id = sale.Id,
            SaleNumber = sale.SaleNumber,
            IsCancelled = sale.IsCancelled
        };
    }
}
