using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.PartnerIntegrations.Responses;

namespace Wrapsfer.Application.PartnerIntegrations.Commands.RotatePartnerIntegrationCredential;

public sealed record RotatePartnerIntegrationCredentialCommand(
    string TenantId) : ICommand<RotatePartnerIntegrationCredentialResponse>;
