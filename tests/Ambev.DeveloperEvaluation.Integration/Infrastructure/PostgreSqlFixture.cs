using Ambev.DeveloperEvaluation.ORM;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Infrastructure;

/// <summary>
/// Spins up a real PostgreSQL container for integration tests.
/// Shared across all test classes in the "PostgreSql" collection via ICollectionFixture.
/// Test classes must use [Collection("PostgreSql")] — do NOT also declare IClassFixture&lt;PostgreSqlFixture&gt;,
/// which would create a second, separate container per class instead of sharing this one.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("testdb")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public DefaultContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DefaultContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new DefaultContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Apply all pending migrations to the fresh container DB.
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
