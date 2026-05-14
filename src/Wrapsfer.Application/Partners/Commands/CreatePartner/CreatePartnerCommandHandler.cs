using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Partners.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.Partners.Commands.CreatePartner;

internal sealed class CreatePartnerCommandHandler(
    IPartnerTenantRepository partnerTenantRepository,
    ITenantSettingsRepository tenantSettingsRepository,
    IIdentityTenantProvisioningService provisioningService,
    IIdentityProviderSettings identityProviderSettings,
    IUnitOfWork unitOfWork,
    ILogger<CreatePartnerCommandHandler> logger) : ICommandHandler<CreatePartnerCommand, PartnerResponse>
{
    public async Task<Result<PartnerResponse>> Handle(CreatePartnerCommand request, CancellationToken cancellationToken)
    {
        string ownerRealm = identityProviderSettings.OwnerRealm;

        if (string.Equals(request.TenantId, ownerRealm, StringComparison.OrdinalIgnoreCase))
        {
            return Result<PartnerResponse>.Failure(PartnerTenantErrors.OwnerRealmNotAllowed);
        }

        bool exists = await partnerTenantRepository.ExistsAsync(request.TenantId, cancellationToken);
        if (exists)
        {
            return Result<PartnerResponse>.Failure(PartnerTenantErrors.AlreadyExists);
        }

        Result<PartnerTenant> createResult = PartnerTenant.Create(
            request.TenantId,
            request.DisplayName,
            ownerRealm,
            request.ContactEmail);

        if (createResult.IsFailure)
        {
            return Result<PartnerResponse>.Failure(createResult.Error);
        }

        PartnerTenant partner = createResult.Value;
        partnerTenantRepository.Add(partner);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        PartnerRealmProvisioningRequest provisioningRequest = new(
            request.TenantId,
            request.DisplayName,
            request.AdminUsername,
            request.AdminEmail,
            request.TemporaryPassword);

        PartnerRealmProvisioningResult provisioningResult =
            await provisioningService.CreatePartnerRealmAsync(provisioningRequest, cancellationToken);

        if (!provisioningResult.Success)
        {
            partner.MarkProvisioningFailed(provisioningResult.FailureReason);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "Partner provisioning failed for tenant {TenantId}",
                request.TenantId);
            return Result<PartnerResponse>.Failure(PartnerTenantErrors.ProvisioningFailed);
        }

        TenantSettingsEntity? existingSettings =
            await tenantSettingsRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (existingSettings is null)
        {
            Result<TenantSettingsEntity> settingsResult = TenantSettingsEntity.Create(request.TenantId);
            if (settingsResult.IsFailure)
            {
                return Result<PartnerResponse>.Failure(settingsResult.Error);
            }

            tenantSettingsRepository.Add(settingsResult.Value);
        }

        Result activationResult = partner.MarkActive();
        if (activationResult.IsFailure)
        {
            return Result<PartnerResponse>.Failure(activationResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PartnerResponse>.Success(PartnerResponse.FromEntity(partner));
    }
}
