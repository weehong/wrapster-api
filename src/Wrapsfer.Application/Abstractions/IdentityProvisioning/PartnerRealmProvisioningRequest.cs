namespace Wrapsfer.Application.Abstractions.IdentityProvisioning;

public sealed record PartnerRealmProvisioningRequest(
    string TenantId,
    string DisplayName,
    string AdminUsername,
    string AdminEmail,
    string TemporaryPassword);
