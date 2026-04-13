using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.TenantSettings.Commands.RemoveRecipient;

public sealed record RemoveRecipientCommand(Guid RecipientId) : ICommand;
