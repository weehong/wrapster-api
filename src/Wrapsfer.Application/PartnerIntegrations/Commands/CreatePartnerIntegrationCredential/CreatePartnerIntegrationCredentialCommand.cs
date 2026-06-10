using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.PartnerIntegrations.Responses;

namespace Wrapsfer.Application.PartnerIntegrations.Commands.CreatePartnerIntegrationCredential;

public sealed record CreatePartnerIntegrationCredentialCommand(
    string TenantId,
    string? DisplayName) : ICommand<CreatePartnerIntegrationCredentialResponse>;
