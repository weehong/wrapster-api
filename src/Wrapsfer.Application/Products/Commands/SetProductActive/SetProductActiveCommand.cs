using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Products.Commands.SetProductActive;

public sealed record SetProductActiveCommand(Guid Id, bool IsActive) : ICommand;
