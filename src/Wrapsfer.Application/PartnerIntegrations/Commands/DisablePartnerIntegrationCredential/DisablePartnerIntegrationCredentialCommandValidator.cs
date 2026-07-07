using FluentValidation;

namespace Wrapsfer.Application.PartnerIntegrations.Commands.DisablePartnerIntegrationCredential;

public sealed class DisablePartnerIntegrationCredentialCommandValidator
    : AbstractValidator<DisablePartnerIntegrationCredentialCommand>
{
    public DisablePartnerIntegrationCredentialCommandValidator() => RuleFor(x => x.TenantId).NotEmpty();
}
