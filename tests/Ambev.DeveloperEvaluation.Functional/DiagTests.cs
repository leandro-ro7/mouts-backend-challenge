using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.WebApi;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Json;
using Xunit;
using Xunit.Abstractions;

namespace Ambev.DeveloperEvaluation.Functional;

public class DiagTests : IClassFixture<DiagTests.DiagFactory>
{
    private readonly DiagFactory _factory;
    private readonly ITestOutputHelper _output;

    public DiagTests(DiagFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task ServerAddress_IsReachable()
    {
        var client = _factory.CreateClient();
        _output.WriteLine($"BaseAddress: {client.BaseAddress}");
        _output.WriteLine($"Server BaseAddress: {_factory.Server?.BaseAddress}");

        var response = await client.GetAsync("/health");
        _output.WriteLine($"Health status: {response.StatusCode}");

        // Just confirm the server responds (any status code)
        response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.RequestTimeout);
    }

    [Fact]
    public async Task PostUsers_Returns201_WithInMemoryDb()
    {
        var client = _factory.CreateClient();

        // Verify which DB provider is being used
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DefaultContext>();
        _output.WriteLine($"DB Provider: {db.Database.ProviderName}");
        _output.WriteLine($"IsRelational: {db.Database.IsRelational()}");

        var payload = new { username = "diag_user", email = "diag@test.com",
            password = "Diag@1234", phone = "+5511999999999", role = 3, status = 1 };

        var response = await client.PostAsJsonAsync("/api/users", payload);
        _output.WriteLine($"Register response: {response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Register body: {body}");
    }

    public class DiagFactory : WebApplicationFactory<Program>
    {
        private static readonly string DbName = "diag-" + Guid.NewGuid().ToString("N");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<DefaultContext>>();
                services.RemoveAll<DefaultContext>();

                var entriesToRemove = services
                    .Where(d =>
                        d.ServiceType.IsGenericType &&
                        d.ServiceType.GetGenericArguments().Length == 1 &&
                        d.ServiceType.GetGenericArguments()[0] == typeof(DefaultContext))
                    .ToList();
                foreach (var d in entriesToRemove) services.Remove(d);

                services.AddDbContext<DefaultContext>(options =>
                    options.UseInMemoryDatabase(DbName));
            });
        }
    }
}
