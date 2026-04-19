using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.CreateSale;

public class CreateSaleCommand : IRequest<CreateSaleResult>
{
    /// <summary>
    /// Optional client-supplied UUID. When provided, a second request with the same key
    /// returns the original sale without creating a duplicate (at-most-once semantics for POST).
    /// </summary>
    public Guid? IdempotencyKey { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public List<CreateSaleItemDto> Items { get; set; } = new();
}
