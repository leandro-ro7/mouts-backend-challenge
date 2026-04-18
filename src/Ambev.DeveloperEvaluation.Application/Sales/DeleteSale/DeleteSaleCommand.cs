using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.DeleteSale;

public class DeleteSaleCommand : IRequest<DeleteSaleResult>
{
    public Guid Id { get; set; }
}
