using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Responses;

namespace Wrapsfer.Application.Shopee.Queries.GetShopeeOrderDetail;

public sealed record GetShopeeOrderDetailQuery(string TenantId, Guid OrderId) : IQuery<ShopeeOrderResponse>;
