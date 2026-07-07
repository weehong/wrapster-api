using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Responses;

namespace Wrapsfer.Application.Shopee.Queries.GetShopeeConnection;

public sealed record GetShopeeConnectionQuery(string TenantId) : IQuery<ShopeeConnectionResponse>;
