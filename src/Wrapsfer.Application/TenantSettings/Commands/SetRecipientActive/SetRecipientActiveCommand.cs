using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.TenantSettings.Commands.SetRecipientActive;

public sealed record SetRecipientActiveCommand(Guid RecipientId, bool IsActive) : ICommand;
