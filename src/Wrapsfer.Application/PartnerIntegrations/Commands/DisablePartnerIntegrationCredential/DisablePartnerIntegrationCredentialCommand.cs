using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.PartnerIntegrations.Responses;

namespace Wrapsfer.Application.PartnerIntegrations.Commands.DisablePartnerIntegrationCredential;

public sealed record DisablePartnerIntegrationCredentialCommand(
    string TenantId) : ICommand<PartnerIntegrationCredentialResponse>;
