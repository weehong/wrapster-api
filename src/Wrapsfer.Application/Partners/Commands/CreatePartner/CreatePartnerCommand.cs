using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Partners.Responses;

namespace Wrapsfer.Application.Partners.Commands.CreatePartner;

public sealed record CreatePartnerCommand(
    string TenantId,
    string DisplayName,
    string AdminEmail,
    string AdminUsername,
    string TemporaryPassword,
    string? ContactEmail = null) : ICommand<PartnerResponse>;
