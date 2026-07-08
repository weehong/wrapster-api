using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Application.Shopee.Common;

internal static class ShopeeProductLinkResponseMapper
{
    public static ShopeeProductLinkResponse Map(ShopeeProductLink link, Product product) =>
        new(
            link.Id,
            link.ProductId,
            product.Name,
            product.Barcode,
            Math.Max(product.AvailableQuantity, 0),
            link.ShopeeItemId,
            link.ShopeeModelId,
            link.ShopeeItemName,
            link.ShopeeModelName,
            link.ShopeeItemSku,
            link.LastSyncedQuantity,
            link.LastSyncedAt,
            link.LastSyncError);
}
