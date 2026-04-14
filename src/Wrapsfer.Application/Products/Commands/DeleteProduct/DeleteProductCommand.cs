using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Products.Commands.DeleteProduct;

public sealed record DeleteProductCommand(Guid Id) : ICommand;
