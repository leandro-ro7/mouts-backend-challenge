using Ambev.DeveloperEvaluation.Domain.Entities;
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
        int page, int size, string? order = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Sales.Include(s => s.Items).AsQueryable();

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
        _context.Sales.Update(sale);
        await _context.SaveChangesAsync(cancellationToken);
        return sale;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sale = await GetByIdAsync(id, cancellationToken);
        if (sale == null) return false;

        _context.Sales.Remove(sale);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
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
