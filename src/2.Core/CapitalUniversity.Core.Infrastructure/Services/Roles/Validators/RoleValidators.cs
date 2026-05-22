using CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;
using FluentValidation;

namespace CapitalUniversity.Core.Infrastructure.Services.Roles.Validators;

public class CreateRoleValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public class UpdateRoleValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleValidator()
    {
        RuleFor(x => x.Name!)
            .NotEmpty()
            .MaximumLength(100)
            .When(x => x.Name != null);
    }
}
