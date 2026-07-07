using FluentValidation;

namespace Wrapsfer.Application.PartnerIntegrations.Commands.RotatePartnerIntegrationCredential;

public sealed class RotatePartnerIntegrationCredentialCommandValidator
    : AbstractValidator<RotatePartnerIntegrationCredentialCommand>
{
    public RotatePartnerIntegrationCredentialCommandValidator() => RuleFor(x => x.TenantId).NotEmpty();
}
