using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.PurchaseOrders.Commands.DeletePurchaseOrder;

public sealed record DeletePurchaseOrderCommand(Guid Id) : ICommand;
