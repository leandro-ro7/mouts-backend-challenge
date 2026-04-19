using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class CreateSaleCommandValidatorTests
{
    private readonly CreateSaleCommandValidator _validator = new();

    private static CreateSaleCommand ValidCommand() => new()
    {
        CustomerId = Guid.NewGuid(),
        CustomerName = "Customer",
        BranchId = Guid.NewGuid(),
        BranchName = "Branch",
        SaleDate = DateTime.UtcNow,
        Items = new List<CreateSaleItemDto>
        {
            new() { ProductId = Guid.NewGuid(), ProductName = "P", Quantity = 5, UnitPrice = 10m }
        }
    };

    [Fact(DisplayName = "Valid command passes validation")]
    public async Task ValidCommand_PassesValidation()
    {
        var result = await _validator.ValidateAsync(ValidCommand());
        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Empty CustomerId fails validation")]
    public async Task EmptyCustomerId_FailsValidation()
    {
        var cmd = ValidCommand();
        cmd.CustomerId = Guid.Empty;
        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(cmd.CustomerId));
    }

    [Fact(DisplayName = "Empty items list fails validation")]
    public async Task EmptyItems_FailsValidation()
    {
        var cmd = ValidCommand();
        cmd.Items.Clear();
        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(cmd.Items));
    }

    [Theory(DisplayName = "Item quantity out of range fails validation")]
    [InlineData(0)]
    [InlineData(21)]
    [InlineData(-1)]
    public async Task ItemQuantityOutOfRange_FailsValidation(int quantity)
    {
        var cmd = ValidCommand();
        cmd.Items[0].Quantity = quantity;
        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Item unit price zero or below fails validation")]
    public async Task ItemUnitPriceZero_FailsValidation()
    {
        var cmd = ValidCommand();
        cmd.Items[0].UnitPrice = 0m;
        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "CustomerName exceeding max length fails validation")]
    public async Task CustomerNameTooLong_FailsValidation()
    {
        var cmd = ValidCommand();
        cmd.CustomerName = new string('X', 151);
        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
    }
}
