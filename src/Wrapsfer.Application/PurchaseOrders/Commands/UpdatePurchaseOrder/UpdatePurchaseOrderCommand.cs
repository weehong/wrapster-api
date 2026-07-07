using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.PurchaseOrders.Commands.UpdatePurchaseOrder;

public sealed record UpdatePurchaseOrderCommand(Guid Id, string PoNumber, int Quantity) : ICommand;
