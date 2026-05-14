using FluentValidation;

namespace Wrapsfer.Application.Partners.Commands.SetPartnerActive;

public sealed class SetPartnerActiveCommandValidator : AbstractValidator<SetPartnerActiveCommand>
{
    public SetPartnerActiveCommandValidator() => RuleFor(x => x.TenantId).NotEmpty();
}
