using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.PurchaseOrders.Responses;

namespace Wrapsfer.Application.PurchaseOrders.Queries.GetPurchaseOrderById;

public sealed record GetPurchaseOrderByIdQuery(Guid Id) : IQuery<PurchaseOrderResponse>;
