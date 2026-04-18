namespace Ambev.DeveloperEvaluation.WebApi.Common;

/// <summary>
/// Paginated result wrapper — built from already-materialized data.
/// IQueryable pagination belongs in the ORM/repository layer, not here.
/// </summary>
public class PaginatedList<T> : List<T>
{
    public int CurrentPage { get; private set; }
    public int TotalPages { get; private set; }
    public int PageSize { get; private set; }
    public int TotalCount { get; private set; }

    public bool HasPrevious => CurrentPage > 1;
    public bool HasNext => CurrentPage < TotalPages;

    public PaginatedList(IEnumerable<T> items, int totalCount, int pageNumber, int pageSize)
    {
        TotalCount = totalCount;
        PageSize = pageSize;
        CurrentPage = pageNumber;
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        AddRange(items);
    }
}
