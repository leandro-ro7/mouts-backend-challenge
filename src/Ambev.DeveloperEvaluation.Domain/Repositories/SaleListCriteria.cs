namespace Ambev.DeveloperEvaluation.Domain.Repositories;

/// <summary>
/// Encapsulates all filter and pagination parameters for a sale list query.
/// Passed as a single object to ISaleRepository.ListAsync so adding new filters
/// requires only extending this record — the interface signature stays stable (OCP).
/// </summary>
public record SaleListCriteria(
    int Page = 1,
    int Size = 10,
    string? Order = null,
    string? CustomerName = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    bool? IsCancelled = null);
