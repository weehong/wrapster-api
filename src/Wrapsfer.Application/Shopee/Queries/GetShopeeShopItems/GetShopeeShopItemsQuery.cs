using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Responses;

namespace Wrapsfer.Application.Shopee.Queries.GetShopeeShopItems;

public sealed record GetShopeeShopItemsQuery(
    string TenantId,
    int Offset,
    int PageSize) : IQuery<ShopeeShopItemsResponse>;
