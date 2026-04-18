using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Sales.CreateSale;

public class CreateSaleCommandValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.CustomerName).NotEmpty().MaximumLength(150);
        RuleFor(c => c.BranchId).NotEmpty();
        RuleFor(c => c.BranchName).NotEmpty().MaximumLength(150);
        RuleFor(c => c.SaleDate).NotEmpty();
        RuleFor(c => c.Items).NotEmpty().WithMessage("A sale must have at least one item.");
        RuleForEach(c => c.Items).SetValidator(new CreateSaleItemDtoValidator());
    }
}

public class CreateSaleItemDtoValidator : AbstractValidator<CreateSaleItemDto>
{
    public CreateSaleItemDtoValidator()
    {
        RuleFor(i => i.ProductId).NotEmpty();
        RuleFor(i => i.ProductName).NotEmpty().MaximumLength(200);
        RuleFor(i => i.Quantity).InclusiveBetween(1, 20)
            .WithMessage("Quantity must be between 1 and 20.");
        RuleFor(i => i.UnitPrice).GreaterThan(0)
            .WithMessage("Unit price must be greater than zero.");
    }
}
