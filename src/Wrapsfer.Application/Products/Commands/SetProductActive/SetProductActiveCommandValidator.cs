using FluentValidation;

namespace Wrapsfer.Application.Products.Commands.SetProductActive;

public sealed class SetProductActiveCommandValidator : AbstractValidator<SetProductActiveCommand>
{
    public SetProductActiveCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}
