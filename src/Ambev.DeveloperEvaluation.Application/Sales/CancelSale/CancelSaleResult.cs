namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSale;

public class CancelSaleResult
{
    public Guid Id { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public bool IsCancelled { get; set; }
}
