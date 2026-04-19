using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Domain.Entities.Sale;

/// <summary>
/// Tests all discount tier boundary values explicitly via the DiscountRate value object.
/// Business rule: "above 4 identical items" → qty > 4 is the threshold for 10% discount.
/// qty 4 = 0% discount (below threshold); qty 5 = first qty eligible for 10%.
/// </summary>
public class SaleItemDiscountTests
{
    [Theory(DisplayName = "Given quantity When resolving DiscountRate Then returns correct tier")]
    [InlineData(1,  0.00)]  // below minimum — no discount
    [InlineData(2,  0.00)]
    [InlineData(3,  0.00)]
    [InlineData(4,  0.00)]  // boundary: still below threshold (rule is "above 4")
    [InlineData(5,  0.10)]  // boundary: first qty with 10% discount
    [InlineData(9,  0.10)]  // boundary: last qty at 10%
    [InlineData(10, 0.20)]  // boundary: first qty at 20%
    [InlineData(11, 0.20)]
    [InlineData(20, 0.20)]  // boundary: max allowed qty, still 20%
    public void DiscountRate_For_ReturnsCorrectTier(int quantity, decimal expectedDiscount)
    {
        var rate = DiscountRate.For(quantity);
        rate.Value.Should().Be(expectedDiscount);
    }

    [Theory(DisplayName = "Given quantity above 20 When resolving DiscountRate Then throws DomainException")]
    [InlineData(21)]
    [InlineData(50)]
    [InlineData(100)]
    public void DiscountRate_For_QuantityAbove20_ThrowsDomainException(int quantity)
    {
        var act = () => DiscountRate.For(quantity);
        act.Should().Throw<DomainException>().WithMessage("*20*");
    }

    [Fact(DisplayName = "DiscountRate named instances have correct values")]
    public void DiscountRate_NamedInstances_HaveCorrectValues()
    {
        DiscountRate.None.Value.Should().Be(0m);
        DiscountRate.TenPercent.Value.Should().Be(0.10m);
        DiscountRate.TwentyPercent.Value.Should().Be(0.20m);
    }

    [Fact(DisplayName = "DiscountRate.Apply correctly reduces amount")]
    public void DiscountRate_Apply_ReducesAmountCorrectly()
    {
        DiscountRate.TenPercent.Apply(100m).Should().Be(90m);
        DiscountRate.TwentyPercent.Apply(100m).Should().Be(80m);
        DiscountRate.None.Apply(100m).Should().Be(100m);
    }
}
