using Wrapsfer.Application.Abstractions;

namespace Wrapsfer.Application.PartnerIntegrations.Responses;

public sealed record CreatePartnerIntegrationCredentialResponse(
    PartnerIntegrationCredentialResponse Credential,
    [property: SensitiveData] string ClientSecret);
