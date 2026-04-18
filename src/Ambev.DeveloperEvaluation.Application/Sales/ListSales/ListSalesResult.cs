using Ambev.DeveloperEvaluation.Application.Sales.GetSale;

namespace Ambev.DeveloperEvaluation.Application.Sales.ListSales;

public class ListSalesResult
{
    /// <summary>Full sale projections — same view as GET /api/sales/{id}.</summary>
    public IEnumerable<GetSaleResult> Data { get; set; } = Enumerable.Empty<GetSaleResult>();
    public int TotalItems { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
}
