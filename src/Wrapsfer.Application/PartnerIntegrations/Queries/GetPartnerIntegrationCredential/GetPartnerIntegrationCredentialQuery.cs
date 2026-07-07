using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.PartnerIntegrations.Responses;

namespace Wrapsfer.Application.PartnerIntegrations.Queries.GetPartnerIntegrationCredential;

public sealed record GetPartnerIntegrationCredentialQuery(
    string TenantId) : IQuery<PartnerIntegrationCredentialResponse>;
