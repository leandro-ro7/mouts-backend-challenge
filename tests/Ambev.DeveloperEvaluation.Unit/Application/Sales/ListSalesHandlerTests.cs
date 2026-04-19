using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using AutoMapper;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class ListSalesHandlerTests
{
    private readonly ISaleRepository _repository;
    private readonly IMapper _mapper;
    private readonly ListSalesHandler _handler;

    public ListSalesHandlerTests()
    {
        _repository = Substitute.For<ISaleRepository>();
        _mapper = Substitute.For<IMapper>();
        _handler = new ListSalesHandler(_repository, _mapper);
    }

    private static Sale MakeSale()
    {
        var s = Sale.Create(Guid.NewGuid(), "C", Guid.NewGuid(), "B", DateTime.UtcNow, Array.Empty<NewSaleItemSpec>());
        s.ClearDomainEvents();
        return s;
    }

    // Helper: match any call to ListAsync regardless of filter args
    private void SetupListAsync(IEnumerable<Sale> sales, int total) =>
        _repository.ListAsync(
            Arg.Any<SaleListCriteria>(),
            Arg.Any<CancellationToken>())
            .Returns((sales, total));

    [Fact(DisplayName = "Given sales in repository When listing Then returns paginated summaries")]
    public async Task Handle_SalesExist_ReturnsPaginatedResult()
    {
        var sales = new List<Sale> { MakeSale(), MakeSale(), MakeSale() };
        SetupListAsync(sales, 3);
        _mapper.Map<IEnumerable<SaleSummaryResult>>(Arg.Any<IEnumerable<Sale>>())
            .Returns(sales.Select(s => new SaleSummaryResult { Id = s.Id }).ToList());

        var result = await _handler.Handle(new ListSalesQuery { Page = 1, Size = 10 }, CancellationToken.None);

        result.Data.Should().HaveCount(3);
        result.TotalItems.Should().Be(3);
        result.CurrentPage.Should().Be(1);
        result.TotalPages.Should().Be(1);
    }

    [Fact(DisplayName = "Given 25 items and page size 10 When listing page 3 Then TotalPages is 3")]
    public async Task Handle_MultiplePages_CalculatesTotalPagesCorrectly()
    {
        SetupListAsync(Enumerable.Empty<Sale>(), 25);
        _mapper.Map<IEnumerable<SaleSummaryResult>>(Arg.Any<IEnumerable<Sale>>())
            .Returns(Enumerable.Empty<SaleSummaryResult>());

        var result = await _handler.Handle(new ListSalesQuery { Page = 3, Size = 10 }, CancellationToken.None);

        result.TotalPages.Should().Be(3);
        result.CurrentPage.Should().Be(3);
        result.TotalItems.Should().Be(25);
    }

    [Fact(DisplayName = "Given empty repository When listing Then returns empty data with zero totals")]
    public async Task Handle_EmptyRepository_ReturnsEmptyResult()
    {
        SetupListAsync(Enumerable.Empty<Sale>(), 0);
        _mapper.Map<IEnumerable<SaleSummaryResult>>(Arg.Any<IEnumerable<Sale>>())
            .Returns(Enumerable.Empty<SaleSummaryResult>());

        var result = await _handler.Handle(new ListSalesQuery { Page = 1, Size = 10 }, CancellationToken.None);

        result.Data.Should().BeEmpty();
        result.TotalItems.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    [Fact(DisplayName = "HasNextPage is true when current page is not the last")]
    public async Task Handle_NotLastPage_HasNextPageIsTrue()
    {
        SetupListAsync(Enumerable.Empty<Sale>(), 25); // 25 items, size 10 → 3 pages
        _mapper.Map<IEnumerable<SaleSummaryResult>>(Arg.Any<IEnumerable<Sale>>())
            .Returns(Enumerable.Empty<SaleSummaryResult>());

        var result = await _handler.Handle(new ListSalesQuery { Page = 2, Size = 10 }, CancellationToken.None);

        result.HasNextPage.Should().BeTrue("page 2 of 3 has a next page");
        result.HasPreviousPage.Should().BeTrue("page 2 of 3 has a previous page");
    }

    [Fact(DisplayName = "HasNextPage is false on last page, HasPreviousPage is false on first page")]
    public async Task Handle_BoundaryPages_HasCorrectFlags()
    {
        SetupListAsync(Enumerable.Empty<Sale>(), 10); // exactly 1 page
        _mapper.Map<IEnumerable<SaleSummaryResult>>(Arg.Any<IEnumerable<Sale>>())
            .Returns(Enumerable.Empty<SaleSummaryResult>());

        var result = await _handler.Handle(new ListSalesQuery { Page = 1, Size = 10 }, CancellationToken.None);

        result.HasNextPage.Should().BeFalse("only one page — no next");
        result.HasPreviousPage.Should().BeFalse("first page — no previous");
    }

    [Fact(DisplayName = "Given filters in query When listing Then repository is called with those filters")]
    public async Task Handle_WithFilters_PassesFiltersToRepository()
    {
        SetupListAsync(Enumerable.Empty<Sale>(), 0);
        _mapper.Map<IEnumerable<SaleSummaryResult>>(Arg.Any<IEnumerable<Sale>>())
            .Returns(Enumerable.Empty<SaleSummaryResult>());

        var from = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var query = new ListSalesQuery
        {
            Page = 1, Size = 5, Order = "saleDate desc",
            CustomerName = "Acme", DateFrom = from, DateTo = to, IsCancelled = false
        };

        await _handler.Handle(query, CancellationToken.None);

        await _repository.Received(1).ListAsync(
            Arg.Is<SaleListCriteria>(c =>
                c.Page == 1 && c.Size == 5 && c.Order == "saleDate desc" &&
                c.CustomerName == "Acme" && c.DateFrom == from && c.DateTo == to &&
                c.IsCancelled == false),
            Arg.Any<CancellationToken>());
    }
}
