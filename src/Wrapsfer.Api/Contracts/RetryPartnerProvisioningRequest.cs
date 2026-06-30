namespace Wrapsfer.Api.Contracts;

public sealed record RetryPartnerProvisioningRequest(
    string DisplayName,
    string AdminEmail,
    string AdminUsername,
    string TemporaryPassword,
    bool IsTemporaryPassword,
    string? ContactEmail = null);
