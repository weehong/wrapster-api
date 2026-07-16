using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Application.Shopee.Common;

internal static class ShopeeOrderResponseMapper
{
    internal static ShopeeOrderResponse Map(ShopeeOrder order) =>
        new(
            order.Id,
            order.OrderSn,
            order.ShopeeStatus,
            order.Status.ToString(),
            order.BuyerUsername,
            order.RecipientName,
            order.TotalAmount,
            order.Currency,
            order.ShippingCarrier,
            order.ShipByDate,
            order.TrackingNumber,
            order.WaybillId,
            order.ShipmentArrangedAt,
            order.LabelPrintedAt,
            order.CancelledAt,
            order.LastShipError,
            order.Items
                .OrderBy(i => i.ShopeeItemId)
                .Select(MapItem)
                .ToList());

    private static ShopeeOrderItemResponse MapItem(ShopeeOrderItem item) =>
        new(
            item.Id,
            item.ShopeeItemId,
            item.ShopeeModelId,
            item.ItemName,
            item.ModelName,
            item.ItemSku,
            item.Quantity,
            item.ProductId);
}
