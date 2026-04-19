using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using Bogus;
using SaleEntity = Ambev.DeveloperEvaluation.Domain.Entities.Sale;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Entities.Sale.TestData;

public static class SaleTestData
{
    private static readonly Faker Faker = new();

    public static SaleEntity CreateValidSale()
    {
        var productId = Faker.Random.Guid();
        var productName = Faker.Commerce.ProductName();
        var unitPrice = Faker.Finance.Amount(1, 100);

        return SaleEntity.Create(
            Faker.Random.Guid(),
            Faker.Name.FullName(),
            Faker.Random.Guid(),
            Faker.Address.City(),
            DateTime.UtcNow,
            new[] { new NewSaleItemSpec(productId, productName, 1, unitPrice) });
    }

    public static SaleEntity CreateSaleWithItem(int quantity, decimal unitPrice = 10m)
    {
        return SaleEntity.Create(
            Faker.Random.Guid(), Faker.Name.FullName(),
            Faker.Random.Guid(), Faker.Address.City(),
            DateTime.UtcNow,
            new[] { new NewSaleItemSpec(Faker.Random.Guid(), Faker.Commerce.ProductName(), quantity, unitPrice) });
    }
}
