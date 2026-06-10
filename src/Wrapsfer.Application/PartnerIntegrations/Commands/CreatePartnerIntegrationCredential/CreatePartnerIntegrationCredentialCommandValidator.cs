using FluentValidation;

namespace Wrapsfer.Application.PartnerIntegrations.Commands.CreatePartnerIntegrationCredential;

public sealed class CreatePartnerIntegrationCredentialCommandValidator
    : AbstractValidator<CreatePartnerIntegrationCredentialCommand>
{
    public CreatePartnerIntegrationCredentialCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.DisplayName).MaximumLength(256);
    }
}
