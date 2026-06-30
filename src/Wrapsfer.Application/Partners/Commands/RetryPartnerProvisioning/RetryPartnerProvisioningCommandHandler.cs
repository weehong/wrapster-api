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

namespace Wrapsfer.Application.Partners.Commands.RetryPartnerProvisioning;

internal sealed class RetryPartnerProvisioningCommandHandler(
    IPartnerTenantRepository partnerTenantRepository,
    ITenantSettingsRepository tenantSettingsRepository,
    IIdentityTenantProvisioningService provisioningService,
    IIdentityProviderSettings identityProviderSettings,
    IUnitOfWork unitOfWork,
    ILogger<RetryPartnerProvisioningCommandHandler> logger)
    : ICommandHandler<RetryPartnerProvisioningCommand, PartnerResponse>
{
    public async Task<Result<PartnerResponse>> Handle(RetryPartnerProvisioningCommand request,
        CancellationToken cancellationToken)
    {
        if (string.Equals(request.TenantId, identityProviderSettings.OwnerRealm, StringComparison.OrdinalIgnoreCase))
        {
            return Result<PartnerResponse>.Failure(PartnerTenantErrors.OwnerRealmNotAllowed);
        }

        PartnerTenant? partner = await partnerTenantRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (partner is null)
        {
            return Result<PartnerResponse>.Failure(PartnerTenantErrors.NotFound);
        }

        Result beginRetry = partner.BeginRetry();
        if (beginRetry.IsFailure)
        {
            return Result<PartnerResponse>.Failure(beginRetry.Error);
        }

        Result profileUpdate = partner.UpdateProfile(request.DisplayName, request.ContactEmail);
        if (profileUpdate.IsFailure)
        {
            return Result<PartnerResponse>.Failure(profileUpdate.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await provisioningService.DeletePartnerRealmAsync(request.TenantId, cancellationToken);

        PartnerRealmProvisioningRequest provisioningRequest = new(
            request.TenantId,
            partner.DisplayName,
            request.AdminUsername,
            request.AdminEmail,
            request.TemporaryPassword,
            request.IsTemporaryPassword);

        PartnerRealmProvisioningResult provisioningResult =
            await provisioningService.CreatePartnerRealmAsync(provisioningRequest, cancellationToken);

        if (!provisioningResult.Success)
        {
            partner.MarkProvisioningFailed(provisioningResult.FailureReason);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "Partner re-provisioning failed for tenant {TenantId}",
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
