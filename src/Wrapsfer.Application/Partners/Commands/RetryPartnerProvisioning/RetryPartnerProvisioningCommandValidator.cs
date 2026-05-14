using FluentValidation;

namespace Wrapsfer.Application.Partners.Commands.RetryPartnerProvisioning;

public sealed class RetryPartnerProvisioningCommandValidator : AbstractValidator<RetryPartnerProvisioningCommand>
{
    public RetryPartnerProvisioningCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();

        RuleFor(x => x.AdminEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(x => x.AdminUsername)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.TemporaryPassword)
            .NotEmpty()
            .MinimumLength(12)
            .WithMessage("Temporary password must be at least 12 characters long.");
    }
}
