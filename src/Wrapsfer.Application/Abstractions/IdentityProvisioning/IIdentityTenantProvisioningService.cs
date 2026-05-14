namespace Wrapsfer.Application.Abstractions.IdentityProvisioning;

public interface IIdentityTenantProvisioningService
{
    Task<PartnerRealmProvisioningResult> CreatePartnerRealmAsync(
        PartnerRealmProvisioningRequest request,
        CancellationToken cancellationToken = default);

    Task<PartnerRealmProvisioningResult> SetPartnerRealmActiveAsync(
        string tenantId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<PartnerRealmProvisioningResult> DeletePartnerRealmAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}
