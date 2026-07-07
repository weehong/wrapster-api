using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.PartnerIntegrations.Responses;

namespace Wrapsfer.Application.PartnerIntegrations.Commands.EnablePartnerIntegrationCredential;

public sealed record EnablePartnerIntegrationCredentialCommand(
    string TenantId) : ICommand<PartnerIntegrationCredentialResponse>;
