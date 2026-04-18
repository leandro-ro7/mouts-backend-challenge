using Ambev.DeveloperEvaluation.Domain.Common;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Ambev.DeveloperEvaluation.ORM;

public class DefaultContext : DbContext
{
    private readonly IEventPublisher? _eventPublisher;
    private readonly ILogger<DefaultContext>? _logger;

    public DbSet<User> Users { get; set; }
    public DbSet<Sale> Sales { get; set; }
    public DbSet<SaleItem> SaleItems { get; set; }

    public DefaultContext(
        DbContextOptions<DefaultContext> options,
        IEventPublisher? eventPublisher = null,
        ILogger<DefaultContext>? logger = null)
        : base(options)
    {
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Collect events before clearing them from entities
        var events = ChangeTracker.Entries<AggregateRoot>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        foreach (var entry in ChangeTracker.Entries<AggregateRoot>())
            entry.Entity.ClearDomainEvents();

        var result = await base.SaveChangesAsync(cancellationToken);

        if (_eventPublisher is not null)
            foreach (var domainEvent in events)
                try
                {
                    await _eventPublisher.PublishAsync(domainEvent, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Data is already committed — log and continue. A dead-letter/outbox
                    // pattern would be needed for at-least-once delivery guarantees.
                    _logger?.LogError(ex,
                        "Failed to publish domain event {EventType}. Data was committed.",
                        domainEvent.GetType().Name);
                }

        return result;
    }
}
public class DefaultContextFactory : IDesignTimeDbContextFactory<DefaultContext>
{
    public DefaultContext CreateDbContext(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var builder = new DbContextOptionsBuilder<DefaultContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        builder.UseNpgsql(
               connectionString,
               b => b.MigrationsAssembly("Ambev.DeveloperEvaluation.ORM")
        );

        return new DefaultContext(builder.Options);
    }
}