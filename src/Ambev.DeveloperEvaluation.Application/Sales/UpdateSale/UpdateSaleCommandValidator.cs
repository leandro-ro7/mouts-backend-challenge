using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;

public class UpdateSaleCommandValidator : AbstractValidator<UpdateSaleCommand>
{
    public UpdateSaleCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.CustomerName).NotEmpty().MaximumLength(150);
        RuleFor(c => c.BranchId).NotEmpty();
        RuleFor(c => c.BranchName).NotEmpty().MaximumLength(150);
        RuleFor(c => c.SaleDate).NotEmpty();
        RuleFor(c => c.Items).NotEmpty().WithMessage("A sale must have at least one item.");
        RuleForEach(c => c.Items).SetValidator(new CreateSaleItemDtoValidator());
    }
}
