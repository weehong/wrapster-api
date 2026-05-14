namespace Wrapsfer.Application.Abstractions.IdentityProvisioning;

public sealed record PartnerRealmProvisioningResult(
    bool Success,
    string? FailureReason = null);
