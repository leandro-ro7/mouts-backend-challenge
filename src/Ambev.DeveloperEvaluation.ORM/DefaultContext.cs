using Ambev.DeveloperEvaluation.Domain.Common;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Text.Json;

namespace Ambev.DeveloperEvaluation.ORM;

public class DefaultContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Sale> Sales { get; set; }
    public DbSet<SaleItem> SaleItems { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    public DefaultContext(DbContextOptions<DefaultContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Collect and clear domain events before the DB transaction.
        var events = ChangeTracker.Entries<AggregateRoot>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        foreach (var entry in ChangeTracker.Entries<AggregateRoot>())
            entry.Entity.ClearDomainEvents();

        // Serialize events into OutboxMessages in the same transaction.
        // The processor dispatches them asynchronously, guaranteeing at-least-once delivery.
        foreach (var domainEvent in events)
        {
            OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                // FullName (no assembly version) is stable across renames/upgrades.
                // The OutboxProcessor resolves the type via its registry using this key.
                EventType = domainEvent.GetType().FullName!,
                Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                OccurredAt = domainEvent.OccurredAt
            });
        }

        return await base.SaveChangesAsync(cancellationToken);
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
