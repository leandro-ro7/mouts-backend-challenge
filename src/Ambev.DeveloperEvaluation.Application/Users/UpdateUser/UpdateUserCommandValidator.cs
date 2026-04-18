using Ambev.DeveloperEvaluation.Domain.Enums;
using Ambev.DeveloperEvaluation.Domain.Validation;
using FluentValidation;

namespace Ambev.DeveloperEvaluation.Application.Users.UpdateUser;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(u => u.Id).NotEmpty();
        RuleFor(u => u.Email).SetValidator(new EmailValidator());
        RuleFor(u => u.Username).NotEmpty().Length(3, 50);
        RuleFor(u => u.Phone).Matches(@"^\+?[1-9]\d{1,14}$");
        RuleFor(u => u.Status).NotEqual(UserStatus.Unknown);
        RuleFor(u => u.Role).NotEqual(UserRole.None);
    }
}
