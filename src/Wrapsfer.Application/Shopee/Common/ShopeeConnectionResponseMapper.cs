using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Application.Shopee.Common;

internal static class ShopeeConnectionResponseMapper
{
    public static ShopeeConnectionResponse Map(ShopeeShopConnection connection) => new(
        connection.ShopId,
        connection.ShopName,
        connection.Region,
        connection.LinkedAt,
        connection.AccessTokenExpiresAt,
        connection.RefreshTokenExpiresAt);
}
