using Bogus;
using SaleEntity = Ambev.DeveloperEvaluation.Domain.Entities.Sale;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Entities.Sale.TestData;

public static class SaleTestData
{
    private static readonly Faker Faker = new();

    public static SaleEntity CreateValidSale()
    {
        var sale = SaleEntity.Create(
            Faker.Random.Guid(),
            Faker.Name.FullName(),
            Faker.Random.Guid(),
            Faker.Address.City(),
            DateTime.UtcNow);

        sale.AddItem(
            Faker.Random.Guid(),
            Faker.Commerce.ProductName(),
            1,
            Faker.Finance.Amount(1, 100));

        return sale;
    }

    public static SaleEntity CreateSaleWithItem(int quantity, decimal unitPrice = 10m)
    {
        var sale = SaleEntity.Create(
            Faker.Random.Guid(), Faker.Name.FullName(),
            Faker.Random.Guid(), Faker.Address.City(),
            DateTime.UtcNow);

        sale.AddItem(Faker.Random.Guid(), Faker.Commerce.ProductName(), quantity, unitPrice);
        return sale;
    }
}
