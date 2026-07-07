using FluentValidation;

namespace Wrapsfer.Application.Partners.Commands.RetryPartnerProvisioning;

public sealed class RetryPartnerProvisioningCommandValidator : AbstractValidator<RetryPartnerProvisioningCommand>
{
    public RetryPartnerProvisioningCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.AdminEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(x => x.AdminUsername)
            .NotEmpty()
            .MinimumLength(AdminUsernamePolicy.MinLength)
            .MaximumLength(AdminUsernamePolicy.MaxLength)
            .Must(AdminUsernamePolicy.HasAllowedCharacters)
            .WithMessage(AdminUsernamePolicy.InvalidCharactersMessage);

        RuleFor(x => x.TemporaryPassword)
            .NotEmpty()
            .MinimumLength(12)
            .WithMessage("Temporary password must be at least 12 characters long.");

        RuleFor(x => x.ContactEmail)
            .EmailAddress()
            .MaximumLength(320)
            .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
    }
}
