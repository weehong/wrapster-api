using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Partners.Responses;

namespace Wrapsfer.Application.Partners.Commands.SetPartnerActive;

public sealed record SetPartnerActiveCommand(string TenantId, bool IsActive) : ICommand<PartnerResponse>;
