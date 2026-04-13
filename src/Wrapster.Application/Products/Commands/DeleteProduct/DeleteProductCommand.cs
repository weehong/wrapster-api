using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.Products.Commands.DeleteProduct;

public sealed record DeleteProductCommand(Guid Id) : ICommand;
