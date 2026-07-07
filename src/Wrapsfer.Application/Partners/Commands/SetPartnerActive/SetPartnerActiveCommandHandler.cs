using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Partners.Responses;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Partners.Commands.SetPartnerActive;

internal sealed class SetPartnerActiveCommandHandler(
    IPartnerTenantRepository partnerTenantRepository,
    IIdentityTenantProvisioningService provisioningService,
    IIdentityProviderSettings identityProviderSettings,
    IRealmConfigurationCache realmConfigurationCache,
    IUnitOfWork unitOfWork,
    ILogger<SetPartnerActiveCommandHandler> logger) : ICommandHandler<SetPartnerActiveCommand, PartnerResponse>
{
    public async Task<Result<PartnerResponse>> Handle(SetPartnerActiveCommand request,
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

        Result stateChange = request.IsActive
            ? partner.Reactivate()
            : partner.Deactivate(DateTime.UtcNow);

        if (stateChange.IsFailure)
        {
            return Result<PartnerResponse>.Failure(stateChange.Error);
        }

        PartnerRealmProvisioningResult provisioningResult = await provisioningService.SetPartnerRealmActiveAsync(
            request.TenantId, request.IsActive, cancellationToken);

        if (!provisioningResult.Success)
        {
            logger.LogWarning(
                "Realm enable={IsActive} failed for tenant {TenantId}; rolling back local state change",
                request.IsActive, request.TenantId);
            return Result<PartnerResponse>.Failure(PartnerTenantErrors.ProvisioningFailed);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        realmConfigurationCache.Remove(request.TenantId);

        return Result<PartnerResponse>.Success(PartnerResponse.FromEntity(partner));
    }
}
