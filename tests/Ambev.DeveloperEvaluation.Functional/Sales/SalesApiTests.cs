using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.WebApi;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Sales;

/// <summary>
/// HTTP-level (functional) tests for the Sales API.
/// Uses <see cref="WebApplicationFactory{TEntryPoint}"/> with an EF Core InMemory database.
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static object DefaultPayload(int quantity = 10) => new
    {
        customerId = Guid.NewGuid(),
        customerName = "Functional Customer",
        branchId = Guid.NewGuid(),
        branchName = "Functional Branch",
        saleDate = DateTime.UtcNow,
        items = new[]
        {
            new { productId = Guid.NewGuid(), productName = "Widget A", quantity, unitPrice = 50m },
            new { productId = Guid.NewGuid(), productName = "Widget B", quantity = 2,  unitPrice = 20m }
        }
    };

    private async Task<(HttpClient client, string token)> AuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndAuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return (client, token);
    }

    // ── POST /api/sales ────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST /api/sales returns 201 with correct discount applied")]
    public async Task CreateSale_Returns201_WithDiscountApplied()
    {
        var (client, _) = await AuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/sales", DefaultPayload(quantity: 10));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");

        data.GetProperty("saleNumber").GetString().Should().NotBeNullOrEmpty();
        // qty=10 → 20% discount: 10 * 50 * 0.80 = 400; qty=2 → no discount: 2 * 20 = 40; total = 440
        data.GetProperty("totalAmount").GetDecimal().Should().Be(440m);
    }

    [Fact(DisplayName = "POST /api/sales response has Location header pointing to GetSaleById route")]
    public async Task CreateSale_Returns201_WithLocationHeader()
    {
        var (client, _) = await AuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/sales", DefaultPayload());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().ContainEquivalentOf("/api/sales/");
    }

    [Fact(DisplayName = "POST /api/sales without auth returns 401")]
    public async Task CreateSale_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/sales", DefaultPayload());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/sales/{id} — verifies NO double-wrapping ─────────────────────

    [Fact(DisplayName = "GET /api/sales/{id} returns 200 with data at root level (no double-wrap)")]
    public async Task GetSale_Returns200_DataAtRootLevel()
    {
        var (client, _) = await AuthenticatedClientAsync();

        // Create a sale first
        var createResponse = await client.PostAsJsonAsync("/api/sales", DefaultPayload(quantity: 4));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var saleId = created.GetProperty("data").GetProperty("id").GetString();

        // Retrieve it
        var getResponse = await client.GetAsync($"/api/sales/{saleId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();

        // Verify NOT double-wrapped: body.data.saleNumber — not body.data.data.saleNumber
        body.TryGetProperty("data", out var data).Should().BeTrue("response must have a 'data' property");
        data.TryGetProperty("saleNumber", out _).Should().BeTrue("data must contain 'saleNumber' directly (no double-wrap)");
        data.TryGetProperty("data", out _).Should().BeFalse("response must NOT be double-wrapped");

        // Verify discount: qty=4 → 10%
        var items = data.GetProperty("items");
        var widgetA = items.EnumerateArray().First(i => i.GetProperty("productName").GetString() == "Widget A");
        widgetA.GetProperty("discount").GetDecimal().Should().Be(0.10m);
    }

    [Fact(DisplayName = "GET /api/sales/{id} returns 404 for unknown id")]
    public async Task GetSale_UnknownId_Returns404()
    {
        var (client, _) = await AuthenticatedClientAsync();
        var response = await client.GetAsync($"/api/sales/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/sales (list) ──────────────────────────────────────────────────

    [Fact(DisplayName = "GET /api/sales returns 200 with paginated summaries (no double-wrap)")]
    public async Task ListSales_Returns200_WithSummaries()
    {
        var (client, _) = await AuthenticatedClientAsync();

        var response = await client.GetAsync("/api/sales?_page=1&_size=5");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("data", out var data).Should().BeTrue();
        data.TryGetProperty("totalItems", out _).Should().BeTrue();
        data.TryGetProperty("currentPage", out _).Should().BeTrue();
    }

    // ── PUT /api/sales/{id} ────────────────────────────────────────────────────

    [Fact(DisplayName = "PUT /api/sales/{id} returns 200 with updated data (no double-wrap)")]
    public async Task UpdateSale_Returns200_WithUpdatedData()
    {
        var (client, _) = await AuthenticatedClientAsync();

        // Create
        var createResponse = await client.PostAsJsonAsync("/api/sales", DefaultPayload(quantity: 4));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var saleId = created.GetProperty("data").GetProperty("id").GetString();
        var rowVersion = created.GetProperty("data").GetProperty("rowVersion").GetUInt32();

        // Update — new items with qty=10 → 20%
        var updatePayload = new
        {
            customerId = Guid.NewGuid(),
            customerName = "Updated Customer",
            branchId = Guid.NewGuid(),
            branchName = "Updated Branch",
            saleDate = DateTime.UtcNow,
            rowVersion,
            items = new[] { new { productId = Guid.NewGuid(), productName = "NewWidget", quantity = 10, unitPrice = 100m } }
        };

        var updateResponse = await client.PutAsJsonAsync($"/api/sales/{saleId}", updatePayload);
        var updateRaw = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, $"Response body: {updateRaw}");

        var body = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(updateRaw);
        body.TryGetProperty("data", out var data).Should().BeTrue();
        data.TryGetProperty("data", out _).Should().BeFalse("update response must NOT be double-wrapped");
        data.GetProperty("customerName").GetString().Should().Be("Updated Customer");
        // qty=10 → 20%: 10 * 100 * 0.80 = 800
        data.GetProperty("totalAmount").GetDecimal().Should().Be(800m);
    }

    [Fact(DisplayName = "PUT /api/sales/{id} with stale rowVersion returns 409 Conflict")]
    public async Task UpdateSale_StaleRowVersion_Returns409()
    {
        var (client, _) = await AuthenticatedClientAsync();

        // Create
        var createResponse = await client.PostAsJsonAsync("/api/sales", DefaultPayload(quantity: 4));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var saleId = created.GetProperty("data").GetProperty("id").GetString();

        // Update with deliberately wrong rowVersion
        var updatePayload = new
        {
            customerId = Guid.NewGuid(),
            customerName = "Updated Customer",
            branchId = Guid.NewGuid(),
            branchName = "Updated Branch",
            saleDate = DateTime.UtcNow,
            rowVersion = 999u,  // stale — sale was just created at version 0
            items = new[] { new { productId = Guid.NewGuid(), productName = "NewWidget", quantity = 5, unitPrice = 50m } }
        };

        var updateResponse = await client.PutAsJsonAsync($"/api/sales/{saleId}", updatePayload);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── PATCH /api/sales/{id}/cancel ──────────────────────────────────────────

    [Fact(DisplayName = "PATCH /api/sales/{id}/cancel returns 200 and sale is cancelled")]
    public async Task CancelSale_Returns200_SaleIsCancelled()
    {
        var (client, _) = await AuthenticatedClientAsync();

        // Create
        var createResponse = await client.PostAsJsonAsync("/api/sales", DefaultPayload());
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var saleId = created.GetProperty("data").GetProperty("id").GetString();

        // Cancel
        var cancelResponse = await client.PatchAsync($"/api/sales/{saleId}/cancel", null);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await cancelResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("data", out var data).Should().BeTrue();
        data.TryGetProperty("data", out _).Should().BeFalse("cancel response must NOT be double-wrapped");

        // Verify the sale is now cancelled
        var getResponse = await client.GetAsync($"/api/sales/{saleId}");
        var getSaleBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        getSaleBody.GetProperty("data").GetProperty("isCancelled").GetBoolean().Should().BeTrue();
    }

    // ── PATCH /api/sales/{id}/items/{itemId}/cancel ───────────────────────────

    [Fact(DisplayName = "PATCH /api/sales/{id}/items/{itemId}/cancel returns 200 and item is cancelled")]
    public async Task CancelSaleItem_Returns200_ItemIsCancelled()
    {
        var (client, _) = await AuthenticatedClientAsync();

        // Create a sale with two items
        var createResponse = await client.PostAsJsonAsync("/api/sales", DefaultPayload(quantity: 5));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var saleId = created.GetProperty("data").GetProperty("id").GetString();

        // Retrieve to get item IDs
        var getResponse = await client.GetAsync($"/api/sales/{saleId}");
        var saleBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var items = saleBody.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        var itemId = items[0].GetProperty("id").GetString();

        // Cancel the first item
        var cancelResponse = await client.PatchAsync($"/api/sales/{saleId}/items/{itemId}/cancel", null);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await cancelResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("data", out var data).Should().BeTrue();
        data.GetProperty("isCancelled").GetBoolean().Should().BeTrue();
    }

    // ── DELETE /api/sales/{id} ────────────────────────────────────────────────

    [Fact(DisplayName = "DELETE /api/sales/{id} returns 200 and sale is cancelled")]
    public async Task DeleteSale_Returns200_SaleIsCancelled()
    {
        var (client, _) = await AuthenticatedClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/sales", DefaultPayload());
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var saleId = created.GetProperty("data").GetProperty("id").GetString();

        var deleteResponse = await client.DeleteAsync($"/api/sales/{saleId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await client.GetAsync($"/api/sales/{saleId}");
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("data").GetProperty("isCancelled").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "DELETE /api/sales/{id} is idempotent — second call returns 200")]
    public async Task DeleteSale_CalledTwice_BothReturn200()
    {
        var (client, _) = await AuthenticatedClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/sales", DefaultPayload());
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var saleId = created.GetProperty("data").GetProperty("id").GetString();

        var first  = await client.DeleteAsync($"/api/sales/{saleId}");
        var second = await client.DeleteAsync($"/api/sales/{saleId}");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK, "DELETE must be idempotent");
    }

    // ── GET /api/sales?isCancelled=true ───────────────────────────────────────

    [Fact(DisplayName = "GET /api/sales with isCancelled=true filter returns only cancelled sales")]
    public async Task ListSales_WithCancelledFilter_ReturnsOnlyCancelledSales()
    {
        var (client, _) = await AuthenticatedClientAsync();

        // Create and immediately cancel one sale
        var createResponse = await client.PostAsJsonAsync("/api/sales", DefaultPayload(quantity: 5));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var saleId = created.GetProperty("data").GetProperty("id").GetString();
        await client.PatchAsync($"/api/sales/{saleId}/cancel", null);

        // Filter by isCancelled=true
        var listResponse = await client.GetAsync("/api/sales?_page=1&_size=50&isCancelled=true");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var dataItems = body.GetProperty("data").GetProperty("data").EnumerateArray().ToList();
        dataItems.Should().NotBeEmpty();
        dataItems.Should().OnlyContain(s => s.GetProperty("isCancelled").GetBoolean());
    }

    // ── Auth helper ───────────────────────────────────────────────────────────

    private async Task<string> RegisterAndAuthenticateAsync(HttpClient client)
    {
        await client.PostAsJsonAsync("/api/users", new
        {
            username = "functional_user",
            email = TestEmail,
            password = TestPassword,
            phone = "+5511999999999",
            role = 3,   // UserRole.Admin
            status = 1  // UserStatus.Active
        });

        var authResponse = await client.PostAsJsonAsync("/api/auth", new
        {
            email = TestEmail,
            password = TestPassword
        });
        authResponse.EnsureSuccessStatusCode();

        var body = await authResponse.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data").GetProperty("token").GetString()!;
    }

    // ── WAF ───────────────────────────────────────────────────────────────────

    public class ApiFactory : WebApplicationFactory<Program>
    {
        private static readonly string DbName = "functional-" + Guid.NewGuid().ToString("N");

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
