namespace Wrapsfer.Api.Contracts;

public sealed record RetryPartnerProvisioningRequest(
    string AdminEmail,
    string AdminUsername,
    string TemporaryPassword);
