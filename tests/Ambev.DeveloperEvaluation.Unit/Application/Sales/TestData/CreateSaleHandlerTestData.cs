using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Bogus;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales.TestData;

public static class CreateSaleHandlerTestData
{
    private static readonly Faker Faker = new();

    public static CreateSaleCommand ValidCommand(int itemQuantity = 1) => new()
    {
        CustomerId = Faker.Random.Guid(),
        CustomerName = Faker.Name.FullName(),
        BranchId = Faker.Random.Guid(),
        BranchName = Faker.Address.City(),
        SaleDate = DateTime.UtcNow,
        Items = new List<CreateSaleItemDto>
        {
            new()
            {
                ProductId = Faker.Random.Guid(),
                ProductName = Faker.Commerce.ProductName(),
                Quantity = itemQuantity,
                UnitPrice = Faker.Finance.Amount(1, 200)
            }
        }
    };

    public static CreateSaleCommand InvalidCommand() => new(); // empty — fails validation
}
