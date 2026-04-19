using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using Ambev.DeveloperEvaluation.Integration.Infrastructure;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Sales;

/// <summary>
/// Integration tests that require a real PostgreSQL instance to validate:
/// - ILIKE query with GIN trigram index
/// - Partial unique index on IdempotencyKey (NULL allowed, duplicate non-NULL rejected)
/// - Optimistic concurrency via RowVersion (IsConcurrencyToken)
/// - AsSplitQuery pagination with real data
/// </summary>
[Collection("PostgreSql")]
public class SaleRepositoryPostgresTests
{
    private readonly PostgreSqlFixture _fixture;

    public SaleRepositoryPostgresTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private static Sale BuildSale(string customerName = "Test Customer", Guid? idempotencyKey = null)
    {
        return Sale.Create(
            Guid.NewGuid(), customerName,
            Guid.NewGuid(), "Branch A",
            DateTime.UtcNow,
            new[] { new NewSaleItemSpec(Guid.NewGuid(), "Product", 4, 10m) },
            idempotencyKey);
    }

    // ─── ILIKE / pg_trgm ────────────────────────────────────────────────────

    [Fact(DisplayName = "CustomerName ILIKE search returns case-insensitive partial match")]
    public async Task ListAsync_CustomerNameFilter_ILikeCaseInsensitive()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new SaleRepository(ctx);

        await repo.CreateAsync(BuildSale("ACME Corporation"), CancellationToken.None);
        await repo.CreateAsync(BuildSale("acme logistics"),   CancellationToken.None);
        await repo.CreateAsync(BuildSale("Unrelated Buyer"),  CancellationToken.None);

        var (items, total) = await repo.ListAsync(new SaleListCriteria(CustomerName: "acme"));

        total.Should().Be(2);
        items.Should().OnlyContain(s => s.CustomerName.Contains("acme", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "CustomerName ILIKE search with mid-string fragment matches correctly")]
    public async Task ListAsync_CustomerNameFilter_MidStringFragment()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new SaleRepository(ctx);

        await repo.CreateAsync(BuildSale("Northern Supplies Ltd"), CancellationToken.None);
        await repo.CreateAsync(BuildSale("Southern Goods"),        CancellationToken.None);
        await repo.CreateAsync(BuildSale("Eastern Exports"),       CancellationToken.None);

        var (items, total) = await repo.ListAsync(new SaleListCriteria(CustomerName: "ern"));

        total.Should().BeGreaterThanOrEqualTo(3, "all three contain 'ern'");
    }

    // ─── IdempotencyKey partial unique index ────────────────────────────────

    [Fact(DisplayName = "IdempotencyKey: two NULLs are allowed by the partial unique index")]
    public async Task Create_TwoNullIdempotencyKeys_BothPersisted()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new SaleRepository(ctx);

        await repo.CreateAsync(BuildSale("Null Key A", idempotencyKey: null), CancellationToken.None);
        await repo.CreateAsync(BuildSale("Null Key B", idempotencyKey: null), CancellationToken.None);

        var count = await ctx.Sales.CountAsync(s => s.IdempotencyKey == null);
        count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact(DisplayName = "IdempotencyKey: duplicate non-NULL key raises unique constraint violation")]
    public async Task Create_DuplicateIdempotencyKey_ThrowsUniqueViolation()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new SaleRepository(ctx);

        var key = Guid.NewGuid();
        await repo.CreateAsync(BuildSale("First",  idempotencyKey: key), CancellationToken.None);

        var act = async () =>
            await repo.CreateAsync(BuildSale("Duplicate", idempotencyKey: key), CancellationToken.None);

        // PostgreSQL raises unique_violation (23505); EF wraps it in DbUpdateException.
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ─── Optimistic concurrency ──────────────────────────────────────────────

    [Fact(DisplayName = "RowVersion: concurrent update on stale version throws ConcurrencyException")]
    public async Task UpdateAsync_StaleRowVersion_ThrowsConcurrencyException()
    {
        // Arrange — persist a sale
        await using var ctxA = _fixture.CreateContext();
        var repoA = new SaleRepository(ctxA);
        var sale  = BuildSale("Concurrency Test");
        var created = await repoA.CreateAsync(sale, CancellationToken.None);
        var saleId  = created.Id;

        // First update — succeeds, increments RowVersion
        var saleV1 = await repoA.GetByIdAsync(saleId, CancellationToken.None);
        saleV1!.UpdateFull(
            Guid.NewGuid(), "Updated Customer",
            Guid.NewGuid(), "Updated Branch",
            DateTime.UtcNow,
            new[] { new NewSaleItemSpec(Guid.NewGuid(), "NewProduct", 2, 5m) });
        await repoA.UpdateAsync(saleV1, CancellationToken.None);

        // Second update on a NEW context (simulates a different request with stale data)
        await using var ctxB = _fixture.CreateContext();
        var repoB = new SaleRepository(ctxB);
        var saleStale = await repoB.GetByIdAsync(saleId, CancellationToken.None);

        // Manually force RowVersion back to simulate a stale read (RowVersion was 0 at creation)
        // EF Core will compare this token against the current DB value and detect the conflict.
        // Instead, we simulate by issuing a concurrent update from ctxA before ctxB saves.
        await using var ctxC = _fixture.CreateContext();
        var saleRace = await ctxC.Sales.Include(s => s.Items).FirstAsync(s => s.Id == saleId);
        saleRace.UpdateFull(
            Guid.NewGuid(), "Race Winner",
            Guid.NewGuid(), "Race Branch",
            DateTime.UtcNow,
            new[] { new NewSaleItemSpec(Guid.NewGuid(), "RaceProduct", 3, 8m) });
        await ctxC.SaveChangesAsync();

        // Now ctxB tries to save with its stale tracked RowVersion
        saleStale!.UpdateFull(
            Guid.NewGuid(), "Late Customer",
            Guid.NewGuid(), "Late Branch",
            DateTime.UtcNow,
            new[] { new NewSaleItemSpec(Guid.NewGuid(), "LateProduct", 1, 5m) });

        var act = async () => await repoB.UpdateAsync(saleStale, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    // ─── Pagination / AsSplitQuery ───────────────────────────────────────────

    [Fact(DisplayName = "ListAsync pagination returns correct page and totalCount")]
    public async Task ListAsync_Pagination_ReturnsCorrectPage()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new SaleRepository(ctx);

        // Seed 5 sales with a distinct customer prefix so the filter isolates them
        var prefix = $"PagTest-{Guid.NewGuid():N}";
        for (var i = 0; i < 5; i++)
            await repo.CreateAsync(BuildSale($"{prefix} Customer {i}"), CancellationToken.None);

        var (page1, total) = await repo.ListAsync(new SaleListCriteria(Page: 1, Size: 2, CustomerName: prefix));
        var (page2, _)     = await repo.ListAsync(new SaleListCriteria(Page: 2, Size: 2, CustomerName: prefix));
        var (page3, _)     = await repo.ListAsync(new SaleListCriteria(Page: 3, Size: 2, CustomerName: prefix));

        total.Should().Be(5);
        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
        page3.Should().HaveCount(1);
    }

    [Fact(DisplayName = "ListAsync order by saleDate desc returns most-recent sale first")]
    public async Task ListAsync_OrderBySaleDateDesc_ReturnsMostRecentFirst()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new SaleRepository(ctx);

        var prefix = $"OrdTest-{Guid.NewGuid():N}";
        await repo.CreateAsync(BuildSale($"{prefix} Old"),    CancellationToken.None);
        await Task.Delay(10); // ensure distinct timestamps
        await repo.CreateAsync(BuildSale($"{prefix} Recent"), CancellationToken.None);

        var (items, _) = await repo.ListAsync(new SaleListCriteria(Order: "saleDate desc", CustomerName: prefix));

        items.First().CustomerName.Should().Contain("Recent");
    }
}
