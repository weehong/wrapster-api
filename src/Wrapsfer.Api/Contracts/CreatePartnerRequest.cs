namespace Wrapsfer.Api.Contracts;

public sealed record CreatePartnerRequest(
    string TenantId,
    string DisplayName,
    string AdminEmail,
    string AdminUsername,
    string TemporaryPassword,
    bool IsTemporaryPassword,
    string? ContactEmail = null);
