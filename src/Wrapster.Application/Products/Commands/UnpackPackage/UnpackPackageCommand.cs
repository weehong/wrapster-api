using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.Products.Commands.UnpackPackage;

public sealed record UnpackPackageCommand(Guid ProductId, int Quantity = 1) : ICommand;
