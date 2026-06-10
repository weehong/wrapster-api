namespace Wrapsfer.Application.Abstractions.IdentityProvisioning;

public interface IIntegrationClientProvisioningService
{
    Task<IntegrationClientProvisioningResult> CreateIntegrationClientAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<IntegrationClientProvisioningResult> RotateIntegrationClientSecretAsync(
        string tenantId,
        string clientUuid,
        CancellationToken cancellationToken = default);

    Task<IntegrationClientProvisioningResult> SetIntegrationClientEnabledAsync(
        string tenantId,
        string clientUuid,
        bool isEnabled,
        CancellationToken cancellationToken = default);

    Task<IntegrationClientProvisioningResult> DeleteIntegrationClientAsync(
        string tenantId,
        string clientUuid,
        CancellationToken cancellationToken = default);
}
