using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.PartnerIntegrations.Common;
using Wrapsfer.Application.PartnerIntegrations.Options;
using Wrapsfer.Application.PartnerIntegrations.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.PartnerIntegrations.Commands.EnablePartnerIntegrationCredential;

internal sealed class EnablePartnerIntegrationCredentialCommandHandler(
    IPartnerIntegrationCredentialRepository credentialRepository,
    IIntegrationClientProvisioningService provisioningService,
    IIdentityProviderSettings identityProviderSettings,
    IOptions<PartnerIntegrationOptions> options,
    IUnitOfWork unitOfWork,
    ILogger<EnablePartnerIntegrationCredentialCommandHandler> logger)
    : ICommandHandler<EnablePartnerIntegrationCredentialCommand, PartnerIntegrationCredentialResponse>
{
    public async Task<Result<PartnerIntegrationCredentialResponse>> Handle(
        EnablePartnerIntegrationCredentialCommand request,
        CancellationToken cancellationToken)
    {
        PartnerIntegrationCredential? credential =
            await credentialRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (credential is null)
        {
            return Result<PartnerIntegrationCredentialResponse>.Failure(
                PartnerIntegrationCredentialErrors.NotFound);
        }

        Result enableResult = credential.Enable();
        if (enableResult.IsFailure)
        {
            return Result<PartnerIntegrationCredentialResponse>.Failure(enableResult.Error);
        }

        IntegrationClientProvisioningResult provisioningResult = await provisioningService
            .SetIntegrationClientEnabledAsync(
                request.TenantId, credential.KeycloakClientUuid, true, cancellationToken);

        if (!provisioningResult.Success)
        {
            logger.LogWarning(
                "Enabling integration client failed for tenant {TenantId}; rolling back local state change",
                request.TenantId);
            return Result<PartnerIntegrationCredentialResponse>.Failure(
                PartnerIntegrationCredentialErrors.IdentityProviderFailure);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PartnerIntegrationCredentialResponse>.Success(
            PartnerIntegrationCredentialResponseMapper.Map(credential, identityProviderSettings, options.Value));
    }
}
