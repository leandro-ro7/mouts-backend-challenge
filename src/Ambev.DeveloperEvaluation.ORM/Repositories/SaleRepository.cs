using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

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

    public async Task<Sale?> GetByNumberAsync(string saleNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.SaleNumber == saleNumber, cancellationToken);
    }

    public async Task<(IEnumerable<Sale> Items, int TotalCount)> ListAsync(
        int page, int size, string? order = null,
        string? customerName = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        bool? isCancelled = null,
        CancellationToken cancellationToken = default)
    {
        // AsSplitQuery avoids the Cartesian explosion from JOIN between Sales and SaleItems.
        var query = _context.Sales.Include(s => s.Items).AsSplitQuery().AsQueryable();

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            // ToLower() on both sides translates to LOWER(col) LIKE '%...%' in PostgreSQL,
            // providing case-insensitive matching without requiring a custom collation.
            var lowerName = customerName.ToLower();
            query = query.Where(s => s.CustomerName.ToLower().Contains(lowerName));
        }

        if (dateFrom.HasValue)
            query = query.Where(s => s.SaleDate >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(s => s.SaleDate <= dateTo.Value);

        if (isCancelled.HasValue)
            query = query.Where(s => s.IsCancelled == isCancelled.Value);

        query = ApplyOrder(query, order);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
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

    private static IQueryable<Sale> ApplyOrder(IQueryable<Sale> query, string? order)
    {
        if (string.IsNullOrWhiteSpace(order))
            return query.OrderByDescending(s => s.CreatedAt);

        var parts = order.Trim('"').Split(',');
        IOrderedQueryable<Sale>? ordered = null;

        foreach (var part in parts)
        {
            var tokens = part.Trim().Split(' ');
            var field = tokens[0].Trim().ToLowerInvariant();
            var desc = tokens.Length > 1 && tokens[1].Trim().Equals("desc", StringComparison.OrdinalIgnoreCase);

            ordered = (field, desc, ordered) switch
            {
                ("salenumber", false, null)  => query.OrderBy(s => s.SaleNumber),
                ("salenumber", true,  null)  => query.OrderByDescending(s => s.SaleNumber),
                ("saledate",   false, null)  => query.OrderBy(s => s.SaleDate),
                ("saledate",   true,  null)  => query.OrderByDescending(s => s.SaleDate),
                ("totalamount",false, null)  => query.OrderBy(s => s.TotalAmount),
                ("totalamount",true,  null)  => query.OrderByDescending(s => s.TotalAmount),
                ("customername",false,null)  => query.OrderBy(s => s.CustomerName),
                ("customername",true, null)  => query.OrderByDescending(s => s.CustomerName),
                ("salenumber", false, not null) => ordered!.ThenBy(s => s.SaleNumber),
                ("salenumber", true,  not null) => ordered!.ThenByDescending(s => s.SaleNumber),
                ("saledate",   false, not null) => ordered!.ThenBy(s => s.SaleDate),
                ("saledate",   true,  not null) => ordered!.ThenByDescending(s => s.SaleDate),
                _ => ordered ?? query.OrderByDescending(s => s.CreatedAt)
            };
        }

        return ordered ?? query.OrderByDescending(s => s.CreatedAt);
    }
}
