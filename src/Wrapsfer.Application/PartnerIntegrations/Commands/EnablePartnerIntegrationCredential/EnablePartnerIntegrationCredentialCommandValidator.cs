using FluentValidation;

namespace Wrapsfer.Application.PartnerIntegrations.Commands.EnablePartnerIntegrationCredential;

public sealed class EnablePartnerIntegrationCredentialCommandValidator
    : AbstractValidator<EnablePartnerIntegrationCredentialCommand>
{
    public EnablePartnerIntegrationCredentialCommandValidator() => RuleFor(x => x.TenantId).NotEmpty();
}
