using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Responses;

namespace Wrapsfer.Application.Shopee.Queries.GetShopeeAuthorizationLink;

public sealed record GetShopeeAuthorizationLinkQuery(
    string TenantId,
    string RedirectUrl) : IQuery<ShopeeAuthorizationLinkResponse>;
