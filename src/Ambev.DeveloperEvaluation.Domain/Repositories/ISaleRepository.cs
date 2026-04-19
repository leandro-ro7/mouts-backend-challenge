using Ambev.DeveloperEvaluation.Domain.Entities;

namespace Ambev.DeveloperEvaluation.Domain.Repositories;

public interface ISaleRepository
{
    Task<Sale> CreateAsync(Sale sale, CancellationToken cancellationToken = default);
    Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Sale?> GetByNumberAsync(string saleNumber, CancellationToken cancellationToken = default);
    Task<(IEnumerable<Sale> Items, int TotalCount)> ListAsync(
        int page, int size, string? order = null,
        string? customerName = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        bool? isCancelled = null,
        CancellationToken cancellationToken = default);
    Task<Sale> UpdateAsync(Sale sale, CancellationToken cancellationToken = default);
}
