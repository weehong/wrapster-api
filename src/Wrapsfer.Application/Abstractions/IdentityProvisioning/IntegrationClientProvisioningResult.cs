namespace Wrapsfer.Application.Abstractions.IdentityProvisioning;

public sealed record IntegrationClientProvisioningResult(
    bool Success,
    string? FailureReason = null,
    string? ClientUuid = null,
    string? ClientId = null,
    [property: SensitiveData] string? ClientSecret = null);
