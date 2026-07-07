using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.TenantSettings.Commands.RemoveRecipient;

public sealed record RemoveRecipientCommand(Guid RecipientId) : ICommand;
