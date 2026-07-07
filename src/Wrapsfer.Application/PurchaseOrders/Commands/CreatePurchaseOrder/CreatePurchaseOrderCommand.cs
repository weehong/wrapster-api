using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.PurchaseOrders.Commands.CreatePurchaseOrder;

public sealed record CreatePurchaseOrderCommand(
    string PoNumber,
    Guid ProductId,
    int Quantity) : ICommand<Guid>;
