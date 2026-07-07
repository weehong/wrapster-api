using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.PurchaseOrders.Commands.RejectPurchaseOrder;

public sealed record RejectPurchaseOrderCommand(Guid Id, string Reason) : ICommand;
