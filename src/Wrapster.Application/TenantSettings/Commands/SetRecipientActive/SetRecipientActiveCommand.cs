using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.TenantSettings.Commands.SetRecipientActive;

public sealed record SetRecipientActiveCommand(Guid RecipientId, bool IsActive) : ICommand;
