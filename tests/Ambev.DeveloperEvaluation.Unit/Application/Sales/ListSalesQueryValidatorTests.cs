using Ambev.DeveloperEvaluation.Application.Sales.ListSales;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class ListSalesQueryValidatorTests
{
    private readonly ListSalesQueryValidator _validator = new();

    private static ListSalesQuery ValidQuery() => new()
    {
        Page = 1,
        Size = 10
    };

    [Fact(DisplayName = "Valid query with defaults passes validation")]
    public async Task ValidQuery_Defaults_PassesValidation()
    {
        var result = await _validator.ValidateAsync(ValidQuery());
        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Page = 0 fails validation")]
    public async Task Page_Zero_FailsValidation()
    {
        var query = ValidQuery();
        query.Page = 0;

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Page");
    }

    [Fact(DisplayName = "Page = -1 fails validation")]
    public async Task Page_Negative_FailsValidation()
    {
        var query = ValidQuery();
        query.Page = -1;

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Page");
    }

    [Fact(DisplayName = "Size = 0 fails validation")]
    public async Task Size_Zero_FailsValidation()
    {
        var query = ValidQuery();
        query.Size = 0;

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Size");
    }

    [Fact(DisplayName = "Size above MaxPageSize fails validation")]
    public async Task Size_AboveMax_FailsValidation()
    {
        var query = ValidQuery();
        query.Size = ListSalesQueryValidator.MaxPageSize + 1;

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Size");
    }

    [Fact(DisplayName = "Size = MaxPageSize passes validation")]
    public async Task Size_AtMax_PassesValidation()
    {
        var query = ValidQuery();
        query.Size = ListSalesQueryValidator.MaxPageSize;

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "DateTo before DateFrom fails validation")]
    public async Task DateTo_BeforeDateFrom_FailsValidation()
    {
        var query = ValidQuery();
        query.DateFrom = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        query.DateTo   = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "DateTo");
    }

    [Fact(DisplayName = "DateTo equal to DateFrom passes validation")]
    public async Task DateTo_EqualToDateFrom_PassesValidation()
    {
        var query = ValidQuery();
        var date = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        query.DateFrom = date;
        query.DateTo   = date;

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "DateTo after DateFrom passes validation")]
    public async Task DateTo_AfterDateFrom_PassesValidation()
    {
        var query = ValidQuery();
        query.DateFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        query.DateTo   = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Only DateFrom set (no DateTo) passes validation")]
    public async Task OnlyDateFrom_NullDateTo_PassesValidation()
    {
        var query = ValidQuery();
        query.DateFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        query.DateTo   = null;

        var result = await _validator.ValidateAsync(query);

        result.IsValid.Should().BeTrue();
    }
}
