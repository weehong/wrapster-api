using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.TenantSettings.Commands.AddRecipient;

public sealed record AddRecipientCommand(string Email) : ICommand<Guid>;
