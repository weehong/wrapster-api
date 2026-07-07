using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Products.Commands.UpdateProductStock;

public sealed record UpdateProductStockCommand(Guid Id, int NewQuantity) : ICommand;
