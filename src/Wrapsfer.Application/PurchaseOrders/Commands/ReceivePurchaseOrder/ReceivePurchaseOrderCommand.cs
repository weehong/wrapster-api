using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.PurchaseOrders.Commands.ReceivePurchaseOrder;

public sealed record ReceivePurchaseOrderCommand(Guid Id) : ICommand;
