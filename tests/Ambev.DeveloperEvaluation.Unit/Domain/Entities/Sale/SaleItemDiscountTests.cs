using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Entities.Sale;

/// <summary>
/// Tests all discount tier boundary values explicitly.
/// Decision documented: "4+" in summary prevails over "above 4" in narrative.
/// qty 4 = 10% discount (inclusive lower bound of first tier).
/// </summary>
public class SaleItemDiscountTests
{
    [Theory(DisplayName = "Given quantity When calculating discount Then returns correct tier")]
    [InlineData(1,  0.00)]  // below minimum — no discount
    [InlineData(2,  0.00)]
    [InlineData(3,  0.00)]  // boundary: last qty without discount
    [InlineData(4,  0.10)]  // boundary: first qty with 10% — "4+" per summary
    [InlineData(5,  0.10)]
    [InlineData(9,  0.10)]  // boundary: last qty at 10%
    [InlineData(10, 0.20)]  // boundary: first qty at 20%
    [InlineData(11, 0.20)]
    [InlineData(20, 0.20)]  // boundary: max allowed qty, still 20%
    public void CalculateDiscount_ReturnsCorrectTier(int quantity, decimal expectedDiscount)
    {
        var discount = SaleItem.CalculateDiscount(quantity);
        discount.Should().Be(expectedDiscount);
    }

    [Theory(DisplayName = "Given quantity above 20 When calculating discount Then throws DomainException")]
    [InlineData(21)]
    [InlineData(50)]
    [InlineData(100)]
    public void CalculateDiscount_QuantityAbove20_ThrowsDomainException(int quantity)
    {
        var act = () => SaleItem.CalculateDiscount(quantity);
        act.Should().Throw<DomainException>()
            .WithMessage("*20*");
    }
}
