using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class UpdateSaleCommandValidatorTests
{
    private readonly UpdateSaleCommandValidator _validator = new();

    private static UpdateSaleCommand ValidCommand() => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        CustomerName = "Customer",
        BranchId = Guid.NewGuid(),
        BranchName = "Branch",
        SaleDate = DateTime.UtcNow,
        Items = new List<UpdateSaleItemDto>
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

    [Fact(DisplayName = "Empty Id fails validation")]
    public async Task EmptyId_FailsValidation()
    {
        var cmd = ValidCommand();
        cmd.Id = Guid.Empty;
        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(cmd.Id));
    }

    [Fact(DisplayName = "Empty items list fails validation")]
    public async Task EmptyItems_FailsValidation()
    {
        var cmd = ValidCommand();
        cmd.Items.Clear();
        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Theory(DisplayName = "Item quantity out of range fails validation")]
    [InlineData(0)]
    [InlineData(21)]
    public async Task ItemQuantityOutOfRange_FailsValidation(int quantity)
    {
        var cmd = ValidCommand();
        cmd.Items[0].Quantity = quantity;
        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
    }
}
