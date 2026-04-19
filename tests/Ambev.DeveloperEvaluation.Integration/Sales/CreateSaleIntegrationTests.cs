using Ambev.DeveloperEvaluation.Application.Sales;
using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.GetSale;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Interceptors;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using AutoMapper;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Sales;

/// <summary>
/// Integration tests using EF Core InMemory database.
/// Tests the full CQRS pipeline: Command → Handler → Repository → DbContext.
/// Event assertions verify the outbox, not a direct publisher mock, because
/// DefaultContext now uses the Transactional Outbox pattern.
/// </summary>
public class CreateSaleIntegrationTests : IDisposable
{
    private readonly DefaultContext _context;
    private readonly ISaleRepository _repository;
    private readonly IMapper _mapper;

    public CreateSaleIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<DefaultContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(new OutboxInterceptor())
            .Options;

        _context = new DefaultContext(options);
        _repository = new SaleRepository(_context);

        _mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<SalesCommonProfile>();
            cfg.AddProfile<CreateSaleProfile>();
            cfg.AddProfile<GetSaleProfile>();
        }).CreateMapper();
    }

    [Fact(DisplayName = "CreateSale + GetSale: full round-trip persists and retrieves correctly")]
    public async Task CreateThenGet_RoundTrip_PersistsAndRetrieves()
    {
        var command = new CreateSaleCommand
        {
            CustomerId = Guid.NewGuid(),
            CustomerName = "Integration Customer",
            BranchId = Guid.NewGuid(),
            BranchName = "Integration Branch",
            SaleDate = DateTime.UtcNow,
            Items = new List<CreateSaleItemDto>
            {
                new() { ProductId = Guid.NewGuid(), ProductName = "Product A", Quantity = 10, UnitPrice = 50m },
                new() { ProductId = Guid.NewGuid(), ProductName = "Product B", Quantity = 3,  UnitPrice = 20m }
            }
        };

        var createHandler = new CreateSaleHandler(_repository, _mapper);
        var getHandler = new GetSaleHandler(_repository, _mapper);

        var created = await createHandler.Handle(command, CancellationToken.None);
        var retrieved = await getHandler.Handle(new GetSaleQuery { Id = created.Id }, CancellationToken.None);

        retrieved.Should().NotBeNull();
        retrieved.CustomerName.Should().Be("Integration Customer");
        retrieved.Items.Should().HaveCount(2);

        var itemA = retrieved.Items.First(i => i.ProductName == "Product A");
        var itemB = retrieved.Items.First(i => i.ProductName == "Product B");
        itemA.Discount.Should().Be(0.20m);  // qty=10 → 20%
        itemB.Discount.Should().Be(0m);      // qty=3  → no discount

        // total = (10 * 50 * 0.80) + (3 * 20) = 400 + 60 = 460
        retrieved.TotalAmount.Should().Be(460m);
        retrieved.SaleNumber.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "CreateSale: SaleCreatedEvent is written to outbox in the same transaction")]
    public async Task CreateSale_WritesSaleCreatedEventToOutbox()
    {
        var command = new CreateSaleCommand
        {
            CustomerId = Guid.NewGuid(),
            CustomerName = "Event Customer",
            BranchId = Guid.NewGuid(),
            BranchName = "Event Branch",
            SaleDate = DateTime.UtcNow,
            Items = new List<CreateSaleItemDto>
            {
                new() { ProductId = Guid.NewGuid(), ProductName = "Product", Quantity = 5, UnitPrice = 100m }
            }
        };

        var handler = new CreateSaleHandler(_repository, _mapper);
        await handler.Handle(command, CancellationToken.None);

        // With the Transactional Outbox pattern, events are stored in OutboxMessages
        // within the same DB transaction. The OutboxProcessor dispatches them later.
        var outbox = await _context.OutboxMessages.ToListAsync();
        outbox.Should().ContainSingle(m =>
            m.EventType.Contains(nameof(SaleCreatedEvent)) && m.ProcessedAt == null);
    }

    [Fact(DisplayName = "CreateSale: quantity above 20 raises DomainException before persisting")]
    public async Task CreateSale_QuantityAbove20_ThrowsDomainException_NothingPersisted()
    {
        var command = new CreateSaleCommand
        {
            CustomerId = Guid.NewGuid(),
            CustomerName = "Customer",
            BranchId = Guid.NewGuid(),
            BranchName = "Branch",
            SaleDate = DateTime.UtcNow,
            Items = new List<CreateSaleItemDto>
            {
                new() { ProductId = Guid.NewGuid(), ProductName = "Product", Quantity = 21, UnitPrice = 10m }
            }
        };

        var handler = new CreateSaleHandler(_repository, _mapper);
        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>().WithMessage("*20*");

        // Domain exception fires before SaveChangesAsync — nothing should be in the DB.
        (await _context.Sales.CountAsync()).Should().Be(0);
        (await _context.OutboxMessages.CountAsync()).Should().Be(0);
    }

    public void Dispose() => _context.Dispose();
}
