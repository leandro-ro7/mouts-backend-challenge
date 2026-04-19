using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Sales.ListSales;

public class ListSalesQueryValidator : AbstractValidator<ListSalesQuery>
{
    public const int MaxPageSize = 100;

    public ListSalesQueryValidator()
    {
        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(q => q.Size)
            .InclusiveBetween(1, MaxPageSize)
            .WithMessage($"Size must be between 1 and {MaxPageSize}.");

        When(q => q.DateFrom.HasValue && q.DateTo.HasValue, () =>
        {
            RuleFor(q => q.DateTo)
                .GreaterThanOrEqualTo(q => q.DateFrom!.Value)
                .WithMessage("DateTo must be greater than or equal to DateFrom.");
        });
    }
}
