using MediatR;

namespace Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;

public class UpdateSaleCommand : IRequest<UpdateSaleResult>
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public List<UpdateSaleItemDto> Items { get; set; } = new();

    /// <summary>The RowVersion the client read — rejected with 409 if the row was modified since.</summary>
    public uint RowVersion { get; set; }
}
