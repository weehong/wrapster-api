using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.TenantSettings.Commands.AddRecipient;

public sealed record AddRecipientCommand(string Email) : ICommand<Guid>;
