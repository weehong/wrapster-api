using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Products.Commands.UnpackPackage;

public sealed record UnpackPackageCommand(Guid ProductId, int Quantity = 1) : ICommand;
