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

namespace Wrapsfer.Application.PartnerIntegrations.Commands.CreatePartnerIntegrationCredential;

internal sealed class CreatePartnerIntegrationCredentialCommandHandler(
    IPartnerTenantRepository partnerTenantRepository,
    IPartnerIntegrationCredentialRepository credentialRepository,
    IIntegrationClientProvisioningService provisioningService,
    IIdentityProviderSettings identityProviderSettings,
    IOptions<PartnerIntegrationOptions> options,
    IUnitOfWork unitOfWork,
    ILogger<CreatePartnerIntegrationCredentialCommandHandler> logger)
    : ICommandHandler<CreatePartnerIntegrationCredentialCommand, CreatePartnerIntegrationCredentialResponse>
{
    public async Task<Result<CreatePartnerIntegrationCredentialResponse>> Handle(
        CreatePartnerIntegrationCredentialCommand request,
        CancellationToken cancellationToken)
    {
        if (string.Equals(request.TenantId, identityProviderSettings.OwnerRealm, StringComparison.OrdinalIgnoreCase))
        {
            return Result<CreatePartnerIntegrationCredentialResponse>.Failure(
                PartnerIntegrationCredentialErrors.OwnerRealmNotAllowed);
        }

        PartnerTenant? partner = await partnerTenantRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (partner is null)
        {
            return Result<CreatePartnerIntegrationCredentialResponse>.Failure(
                PartnerIntegrationCredentialErrors.PartnerTenantNotFound);
        }

        bool exists = await credentialRepository.ExistsForTenantAsync(request.TenantId, cancellationToken);
        if (exists)
        {
            return Result<CreatePartnerIntegrationCredentialResponse>.Failure(
                PartnerIntegrationCredentialErrors.AlreadyExists);
        }

        IntegrationClientProvisioningResult provisioningResult =
            await provisioningService.CreateIntegrationClientAsync(request.TenantId, cancellationToken);

        if (!provisioningResult.Success
            || string.IsNullOrWhiteSpace(provisioningResult.ClientUuid)
            || string.IsNullOrWhiteSpace(provisioningResult.ClientId)
            || string.IsNullOrWhiteSpace(provisioningResult.ClientSecret))
        {
            logger.LogWarning(
                "Integration client provisioning failed for tenant {TenantId}",
                request.TenantId);
            return Result<CreatePartnerIntegrationCredentialResponse>.Failure(
                PartnerIntegrationCredentialErrors.IdentityProviderFailure);
        }

        string displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? $"{partner.DisplayName} Integration"
            : request.DisplayName;

        Result<PartnerIntegrationCredential> createResult = PartnerIntegrationCredential.Create(
            request.TenantId,
            provisioningResult.ClientId,
            provisioningResult.ClientUuid,
            displayName);

        if (createResult.IsFailure)
        {
            await CleanUpIdentityClientAsync(request.TenantId, provisioningResult.ClientUuid, cancellationToken);
            return Result<CreatePartnerIntegrationCredentialResponse>.Failure(createResult.Error);
        }

        PartnerIntegrationCredential credential = createResult.Value;
        credentialRepository.Add(credential);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogCritical(ex,
                "Integration credential metadata save failed after Keycloak client creation for tenant {TenantId}; cleaning up identity client",
                request.TenantId);
            await CleanUpIdentityClientAsync(request.TenantId, provisioningResult.ClientUuid, cancellationToken);
            return Result<CreatePartnerIntegrationCredentialResponse>.Failure(
                PartnerIntegrationCredentialErrors.CreationFailed);
        }

        PartnerIntegrationCredentialResponse metadata = PartnerIntegrationCredentialResponseMapper.Map(
            credential, identityProviderSettings, options.Value);

        return Result<CreatePartnerIntegrationCredentialResponse>.Success(
            new CreatePartnerIntegrationCredentialResponse(metadata, provisioningResult.ClientSecret));
    }

    private async Task CleanUpIdentityClientAsync(
        string tenantId,
        string clientUuid,
        CancellationToken cancellationToken)
    {
        IntegrationClientProvisioningResult deleteResult =
            await provisioningService.DeleteIntegrationClientAsync(tenantId, clientUuid, cancellationToken);

        if (!deleteResult.Success)
        {
            logger.LogCritical(
                "Orphaned Keycloak integration client {ClientUuid} could not be cleaned up for tenant {TenantId}",
                clientUuid, tenantId);
        }
    }
}
