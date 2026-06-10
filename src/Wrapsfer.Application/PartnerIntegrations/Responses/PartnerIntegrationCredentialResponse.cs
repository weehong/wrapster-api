using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Application.PartnerIntegrations.Responses;

public sealed record PartnerIntegrationCredentialResponse(
    string TenantId,
    string ClientId,
    string DisplayName,
    string TokenUrl,
    string ApiBaseUrl,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? LastRotatedAt,
    DateTime? DisabledAt)
{
    public static PartnerIntegrationCredentialResponse FromEntity(
        PartnerIntegrationCredential entity,
        string tokenUrl,
        string apiBaseUrl) => new(
        entity.TenantId,
        entity.ClientId,
        entity.DisplayName,
        tokenUrl,
        apiBaseUrl,
        entity.IsEnabled,
        entity.CreatedAt,
        entity.LastRotatedAt,
        entity.DisabledAt);
}
