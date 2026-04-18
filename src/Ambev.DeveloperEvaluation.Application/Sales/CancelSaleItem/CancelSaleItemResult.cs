namespace Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;

public class CancelSaleItemResult
{
    public Guid SaleId { get; set; }
    public Guid ItemId { get; set; }
    public decimal NewSaleTotalAmount { get; set; }
}
