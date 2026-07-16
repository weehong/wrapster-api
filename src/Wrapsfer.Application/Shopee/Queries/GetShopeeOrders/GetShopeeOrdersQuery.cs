using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Shopee.Queries.GetShopeeOrders;

public sealed record GetShopeeOrdersQuery(
    string TenantId,
    ShopeeOrderStatus? Status,
    string? Search,
    int Page,
    int PageSize) : IQuery<ShopeeOrdersResponse>;
