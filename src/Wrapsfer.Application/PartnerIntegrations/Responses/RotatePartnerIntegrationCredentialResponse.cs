using Wrapsfer.Application.Abstractions;

namespace Wrapsfer.Application.PartnerIntegrations.Responses;

public sealed record RotatePartnerIntegrationCredentialResponse(
    PartnerIntegrationCredentialResponse Credential,
    [property: SensitiveData] string ClientSecret);
