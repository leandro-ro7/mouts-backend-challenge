using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Ambev.DeveloperEvaluation.ORM.Repositories;

public class SaleRepository : ISaleRepository
{
    private readonly DefaultContext _context;

    public SaleRepository(DefaultContext context)
    {
        _context = context;
    }

    public async Task<Sale> CreateAsync(Sale sale, CancellationToken cancellationToken = default)
    {
        await _context.Sales.AddAsync(sale, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return sale;
    }

    public async Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Sale?> GetByIdempotencyKeyAsync(Guid key, CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.IdempotencyKey == key, cancellationToken);
    }

    public async Task<(IEnumerable<Sale> Items, int TotalCount)> ListAsync(
        SaleListCriteria criteria, CancellationToken cancellationToken = default)
    {
        // AsSplitQuery avoids the Cartesian explosion from JOIN between Sales and SaleItems.
        var query = _context.Sales.Include(s => s.Items).AsSplitQuery().AsQueryable();

        if (!string.IsNullOrWhiteSpace(criteria.CustomerName))
        {
            // EF.Functions.ILike translates to PostgreSQL ILIKE, which is case-insensitive and
            // can use a GIN index with gin_trgm_ops for wildcard-prefix patterns (%value%).
            query = query.Where(s => EF.Functions.ILike(s.CustomerName, $"%{criteria.CustomerName}%"));
        }

        if (criteria.DateFrom.HasValue)
            query = query.Where(s => s.SaleDate >= criteria.DateFrom.Value);

        if (criteria.DateTo.HasValue)
            query = query.Where(s => s.SaleDate <= criteria.DateTo.Value);

        if (criteria.IsCancelled.HasValue)
            query = query.Where(s => s.IsCancelled == criteria.IsCancelled.Value);

        query = ApplyOrder(query, criteria.Order);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((criteria.Page - 1) * criteria.Size)
            .Take(criteria.Size)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Sale> UpdateAsync(Sale sale, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return sale;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException(
                $"Sale {sale.Id} was modified by a concurrent request. Reload and retry.", ex);
        }
    }

    private static readonly Dictionary<string, Expression<Func<Sale, object>>> SortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["salenumber"]   = s => s.SaleNumber,
            ["saledate"]     = s => s.SaleDate,
            ["totalamount"]  = s => s.TotalAmount,
            ["customername"] = s => s.CustomerName,
        };

    private static IQueryable<Sale> ApplyOrder(IQueryable<Sale> query, string? order)
    {
        if (string.IsNullOrWhiteSpace(order))
            return query.OrderByDescending(s => s.CreatedAt);

        IOrderedQueryable<Sale>? result = null;

        foreach (var part in order.Trim('"').Split(','))
        {
            var tokens = part.Trim().Split(' ', 2);
            var field = tokens[0].Trim();
            var descending = tokens.Length > 1 &&
                tokens[1].Trim().Equals("desc", StringComparison.OrdinalIgnoreCase);

            if (!SortFields.TryGetValue(field, out var keySelector))
                continue;

            result = (result, descending) switch
            {
                (null,     false) => query.OrderBy(keySelector),
                (null,     true)  => query.OrderByDescending(keySelector),
                (not null, false) => result.ThenBy(keySelector),
                (not null, true)  => result.ThenByDescending(keySelector),
            };
        }

        return result ?? query.OrderByDescending(s => s.CreatedAt);
    }
}
