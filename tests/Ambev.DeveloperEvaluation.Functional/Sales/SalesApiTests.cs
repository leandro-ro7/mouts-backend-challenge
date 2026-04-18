using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

namespace Ambev.DeveloperEvaluation.Functional.Sales;

/// <summary>
/// HTTP-level (functional) tests for the Sales API.
/// Uses <see cref="WebApplicationFactory{TEntryPoint}"/> to spin up the full ASP.NET Core
/// pipeline with an EF Core InMemory database substituted for PostgreSQL.
/// </summary>
public class SalesApiTests : IClassFixture<SalesApiTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    private const string TestEmail = "functional@test.com";
    private const string TestPassword = "Functional@1234";

    public SalesApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "POST /api/sales returns 201 with correct discount applied")]
    public async Task CreateSale_Returns201_WithDiscountApplied()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndAuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            customerId = Guid.NewGuid(),
            customerName = "Functional Customer",
            branchId = Guid.NewGuid(),
            branchName = "Functional Branch",
            saleDate = DateTime.UtcNow,
            items = new[]
            {
                new { productId = Guid.NewGuid(), productName = "Widget A", quantity = 10, unitPrice = 50m },
                new { productId = Guid.NewGuid(), productName = "Widget B", quantity = 2,  unitPrice = 20m }
            }
        };

        var response = await client.PostAsJsonAsync("/api/sales", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");

        data.GetProperty("saleNumber").GetString().Should().NotBeNullOrEmpty();
        // qty=10 → 20% discount: 10 * 50 * 0.80 = 400; qty=2 → no discount: 2 * 20 = 40; total = 440
        data.GetProperty("totalAmount").GetDecimal().Should().Be(440m);
    }

    [Fact(DisplayName = "POST /api/sales without auth returns 401")]
    public async Task CreateSale_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var payload = new
        {
            customerId = Guid.NewGuid(), customerName = "X",
            branchId = Guid.NewGuid(), branchName = "X",
            saleDate = DateTime.UtcNow, items = Array.Empty<object>()
        };

        var response = await client.PostAsJsonAsync("/api/sales", payload);

        // 401 Unauthorized (auth middleware blocks unauthenticated requests)
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<string> RegisterAndAuthenticateAsync(HttpClient client)
    {
        // Register via public POST /api/users (AllowAnonymous)
        var registerPayload = new
        {
            username = "functional_user",
            email = TestEmail,
            password = TestPassword,
            phone = "+5511999999999",
            role = 3,   // UserRole.Admin
            status = 1  // UserStatus.Active
        };
        var registerResponse = await client.PostAsJsonAsync("/api/users", registerPayload);
        registerResponse.EnsureSuccessStatusCode();

        // Authenticate and return JWT
        var authResponse = await client.PostAsJsonAsync("/api/auth", new
        {
            email = TestEmail,
            password = TestPassword
        });

        authResponse.EnsureSuccessStatusCode();
        var body = await authResponse.Content.ReadFromJsonAsync<JsonElement>();

        var obj = JsonSerializer.Deserialize<Root>(body);
        var token = obj?.data?.data?.token;
        return token.ToString();
    }

    public class ApiFactory : WebApplicationFactory<Program>
    {
        // Static name ensures all scopes share the same InMemory database
        private static readonly string DbName = "functional-" + Guid.NewGuid().ToString("N");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            // ConfigureTestServices runs AFTER the app's service registration,
            // so these registrations take priority
            builder.ConfigureTestServices(services =>
            {
                // Remove ALL existing DbContext registrations for DefaultContext
                services.RemoveAll<DbContextOptions<DefaultContext>>();
                services.RemoveAll<DefaultContext>();

                // Remove all IDbContextOptionsConfiguration<DefaultContext> entries
                // (EF Core uses these to build the final DbContextOptions)
                var entriesToRemove = services
                    .Where(d =>
                        d.ServiceType.IsGenericType &&
                        d.ServiceType.GetGenericArguments().Length == 1 &&
                        d.ServiceType.GetGenericArguments()[0] == typeof(DefaultContext))
                    .ToList();
                foreach (var d in entriesToRemove) services.Remove(d);

                // Re-register with InMemory provider
                services.AddDbContext<DefaultContext>(options =>
                    options.UseInMemoryDatabase(DbName));
            });
        }
    }

    public class Root
    {
        public InnerData data { get; set; }
    }

    public class InnerData
    {
        public TokenData data { get; set; }
    }

    public class TokenData
    {
        public string token { get; set; }
    }
}
