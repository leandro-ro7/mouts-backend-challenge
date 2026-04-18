using Ambev.DeveloperEvaluation.Domain.Repositories;
using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;

public class CancelSaleItemHandler : IRequestHandler<CancelSaleItemCommand, CancelSaleItemResult>
{
    private readonly ISaleRepository _repository;

    public CancelSaleItemHandler(ISaleRepository repository)
    {
        _repository = repository;
    }

    public async Task<CancelSaleItemResult> Handle(CancelSaleItemCommand command, CancellationToken cancellationToken)
    {
        var sale = await _repository.GetByIdAsync(command.SaleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Sale with ID {command.SaleId} was not found.");

        var cancelledItem = sale.CancelItem(command.ItemId); // raises ItemCancelledEvent internally

        await _repository.UpdateAsync(sale, cancellationToken);

        return new CancelSaleItemResult
        {
            SaleId = sale.Id,
            ItemId = cancelledItem.Id,
            NewSaleTotalAmount = sale.TotalAmount
        };
    }
}
