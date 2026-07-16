using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Responses;

namespace Wrapsfer.Application.Shopee.Queries.GetShopeeShippingParameter;

public sealed record GetShopeeShippingParameterQuery(string TenantId, Guid OrderId)
    : IQuery<ShopeeShippingParameterResponse>;
