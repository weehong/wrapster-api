using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Responses;

namespace Wrapsfer.Application.Shopee.Queries.GetShopeeOrderLabel;

public sealed record GetShopeeOrderLabelQuery(string TenantId, Guid OrderId) : IQuery<ShopeeOrderLabelResult>;
