using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions;
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

namespace Wrapsfer.Application.PartnerIntegrations.Commands.RotatePartnerIntegrationCredential;

internal sealed class RotatePartnerIntegrationCredentialCommandHandler(
    IPartnerIntegrationCredentialRepository credentialRepository,
    IIntegrationClientProvisioningService provisioningService,
    IIdentityProviderSettings identityProviderSettings,
    ITenantContext tenantContext,
    IOptions<PartnerIntegrationOptions> options,
    IUnitOfWork unitOfWork,
    ILogger<RotatePartnerIntegrationCredentialCommandHandler> logger)
    : ICommandHandler<RotatePartnerIntegrationCredentialCommand, RotatePartnerIntegrationCredentialResponse>
{
    public async Task<Result<RotatePartnerIntegrationCredentialResponse>> Handle(
        RotatePartnerIntegrationCredentialCommand request,
        CancellationToken cancellationToken)
    {
        PartnerIntegrationCredential? credential =
            await credentialRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (credential is null)
        {
            return Result<RotatePartnerIntegrationCredentialResponse>.Failure(
                PartnerIntegrationCredentialErrors.NotFound);
        }

        if (!credential.IsEnabled)
        {
            return Result<RotatePartnerIntegrationCredentialResponse>.Failure(
                PartnerIntegrationCredentialErrors.Disabled);
        }

        IntegrationClientProvisioningResult rotationResult = await provisioningService
            .RotateIntegrationClientSecretAsync(request.TenantId, credential.KeycloakClientUuid, cancellationToken);

        if (!rotationResult.Success || string.IsNullOrWhiteSpace(rotationResult.ClientSecret))
        {
            logger.LogWarning(
                "Integration client secret rotation failed for tenant {TenantId}",
                request.TenantId);
            return Result<RotatePartnerIntegrationCredentialResponse>.Failure(
                PartnerIntegrationCredentialErrors.IdentityProviderFailure);
        }

        Result markResult = credential.MarkRotated(DateTime.UtcNow, tenantContext.Username);
        if (markResult.IsFailure)
        {
            return Result<RotatePartnerIntegrationCredentialResponse>.Failure(markResult.Error);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The old secret is already invalidated in Keycloak; failing the request here would
            // leave the partner with no working secret at all. Surface the new secret and flag
            // the stale rotation metadata for operators.
            logger.LogCritical(ex,
                "Integration credential rotation metadata save failed for tenant {TenantId}; Keycloak secret was rotated",
                request.TenantId);
        }

        PartnerIntegrationCredentialResponse metadata = PartnerIntegrationCredentialResponseMapper.Map(
            credential, identityProviderSettings, options.Value);

        return Result<RotatePartnerIntegrationCredentialResponse>.Success(
            new RotatePartnerIntegrationCredentialResponse(metadata, rotationResult.ClientSecret));
    }
}
