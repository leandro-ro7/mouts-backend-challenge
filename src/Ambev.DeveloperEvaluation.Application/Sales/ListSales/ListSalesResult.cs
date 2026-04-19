namespace Ambev.DeveloperEvaluation.Application.Sales.ListSales;

public class ListSalesResult
{
    public IEnumerable<SaleSummaryResult> Data { get; set; } = Enumerable.Empty<SaleSummaryResult>();
    public int TotalItems { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
}
