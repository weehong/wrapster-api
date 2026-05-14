using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Partners.Responses;

namespace Wrapsfer.Application.Partners.Commands.RetryPartnerProvisioning;

public sealed record RetryPartnerProvisioningCommand(
    string TenantId,
    string AdminEmail,
    string AdminUsername,
    string TemporaryPassword,
    bool IsTemporaryPassword) : ICommand<PartnerResponse>;
