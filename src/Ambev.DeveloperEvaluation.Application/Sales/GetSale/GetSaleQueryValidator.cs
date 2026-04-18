using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Sales.GetSale;

public class GetSaleQueryValidator : AbstractValidator<GetSaleQuery>
{
    public GetSaleQueryValidator()
    {
        RuleFor(q => q.Id)
            .NotEmpty()
            .WithMessage("Sale ID must not be empty.");
    }
}
