using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Application.PartnerIntegrations.Options;
using Wrapsfer.Application.PartnerIntegrations.Responses;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Application.PartnerIntegrations.Common;

internal static class PartnerIntegrationCredentialResponseMapper
{
    public static PartnerIntegrationCredentialResponse Map(
        PartnerIntegrationCredential credential,
        IIdentityProviderSettings identityProviderSettings,
        PartnerIntegrationOptions options) =>
        PartnerIntegrationCredentialResponse.FromEntity(
            credential,
            identityProviderSettings.GetTokenUrl(credential.TenantId),
            options.ApiBaseUrl);
}
