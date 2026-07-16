namespace Wrapsfer.Application.Shopee.Responses;

public sealed record ShopeeOrderResponse(
    Guid Id,
    string OrderSn,
    string ShopeeStatus,
    string Status,
    string? BuyerUsername,
    string? RecipientName,
    decimal TotalAmount,
    string? Currency,
    string? ShippingCarrier,
    DateTime? ShipByDate,
    string? TrackingNumber,
    Guid? WaybillId,
    DateTime? ShipmentArrangedAt,
    DateTime? LabelPrintedAt,
    DateTime? CancelledAt,
    string? LastShipError,
    IReadOnlyList<ShopeeOrderItemResponse> Items);
