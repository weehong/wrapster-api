using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.Products.Commands.UpdateProductStock;

public sealed record UpdateProductStockCommand(Guid Id, int NewQuantity) : ICommand;
