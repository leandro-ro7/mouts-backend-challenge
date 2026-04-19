namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

public class UpdateSaleRequest
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public List<UpdateSaleItemRequest> Items { get; set; } = new();

    /// <summary>The RowVersion received from the last GET. Required to detect concurrent modifications.</summary>
    public uint RowVersion { get; set; }
}

public class UpdateSaleItemRequest
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
