using Ambev.DeveloperEvaluation.Domain.Repositories;
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

        sale.Update(command.CustomerId, command.CustomerName,
            command.BranchId, command.BranchName, command.SaleDate); // raises SaleModifiedEvent internally

        foreach (var existingItem in sale.Items.Where(i => !i.IsCancelled).ToList())
            sale.CancelItem(existingItem.Id);

        foreach (var item in command.Items)
            sale.AddItem(item.ProductId, item.ProductName, item.Quantity, item.UnitPrice);

        var updated = await _repository.UpdateAsync(sale, cancellationToken);

        return _mapper.Map<UpdateSaleResult>(updated);
    }
}
