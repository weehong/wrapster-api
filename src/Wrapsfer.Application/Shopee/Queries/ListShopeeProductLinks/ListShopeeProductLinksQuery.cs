using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Responses;

namespace Wrapsfer.Application.Shopee.Queries.ListShopeeProductLinks;

public sealed record ListShopeeProductLinksQuery(
    string TenantId) : IQuery<IReadOnlyList<ShopeeProductLinkResponse>>;
