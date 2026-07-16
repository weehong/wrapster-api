using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Errors;

public static class ShopeeOrderErrors
{
    public static readonly Error NotFound = new(
        "ShopeeOrder.NotFound", "The Shopee order was not found", ErrorType.NotFound);

    public static readonly Error InvalidTenantId = new(
        "ShopeeOrder.InvalidTenantId", "Tenant ID is required", ErrorType.Validation);

    public static readonly Error InvalidOrderSn = new(
        "ShopeeOrder.InvalidOrderSn", "Order serial number is required", ErrorType.Validation);

    public static readonly Error NoItems = new(
        "ShopeeOrder.NoItems", "A Shopee order must have at least one item", ErrorType.Validation);

    public static readonly Error InvalidQuantity = new(
        "ShopeeOrder.InvalidQuantity", "Item quantity must be greater than zero", ErrorType.Validation);

    public static readonly Error InvalidShopeeStatus = new(
        "ShopeeOrder.InvalidShopeeStatus", "Shopee order status is required", ErrorType.Validation);

    public static readonly Error NotReadyToShip = new(
        "ShopeeOrder.NotReadyToShip",
        "Shipment can only be arranged for orders that are ready to ship",
        ErrorType.Conflict);

    public static readonly Error ItemsNotLinked = new(
        "ShopeeOrder.ItemsNotLinked",
        "All order items must be linked to Wrapsfer products before arranging shipment",
        ErrorType.Conflict);

    public static readonly Error NotAwaitingTracking = new(
        "ShopeeOrder.NotAwaitingTracking",
        "The order is not awaiting a tracking number",
        ErrorType.Conflict);

    public static readonly Error InvalidTrackingNumber = new(
        "ShopeeOrder.InvalidTrackingNumber",
        "Tracking number is required and must be at most 100 characters",
        ErrorType.Validation);

    public static readonly Error TrackingNotAssigned = new(
        "ShopeeOrder.TrackingNotAssigned",
        "A tracking number must be assigned before linking a waybill",
        ErrorType.Conflict);

    public static readonly Error CannotFailShipment = new(
        "ShopeeOrder.CannotFailShipment",
        "Shipment failure can only be recorded while shipment is being arranged",
        ErrorType.Conflict);

    public static readonly Error InvalidWaybillId = new(
        "ShopeeOrder.InvalidWaybillId", "Waybill ID is required", ErrorType.Validation);

    public static readonly Error AlreadyCancelled = new(
        "ShopeeOrder.AlreadyCancelled", "The Shopee order is already cancelled", ErrorType.Conflict);

    public static readonly Error NotShipped = new(
        "ShopeeOrder.NotShipped",
        "The shipping label is only available after shipment is arranged and tracked",
        ErrorType.Conflict);

    public static readonly Error InvalidLabelObjectKey = new(
        "ShopeeOrder.InvalidLabelObjectKey", "Label object key is required", ErrorType.Validation);

    public static readonly Error InvalidShipError = new(
        "ShopeeOrder.InvalidShipError", "Ship error text is required", ErrorType.Validation);

    public static readonly Error ConnectionNotFound = new(
        "ShopeeOrder.ConnectionNotFound",
        "No Shopee shop is linked to this tenant",
        ErrorType.NotFound);

    public static readonly Error OrderDetailFetchFailed = new(
        "ShopeeOrder.OrderDetailFetchFailed",
        "Shopee did not return the order detail",
        ErrorType.Failure);

    public static readonly Error OrderListFetchFailed = new(
        "ShopeeOrder.OrderListFetchFailed",
        "Shopee did not return the order list",
        ErrorType.Failure);

    public static readonly Error ShippingParameterFetchFailed = new(
        "ShopeeOrder.ShippingParameterFetchFailed",
        "Shopee did not return the shipping options for this order",
        ErrorType.Failure);

    public static readonly Error ShipmentRequestFailed = new(
        "ShopeeOrder.ShipmentRequestFailed",
        "Shopee rejected the shipment arrangement",
        ErrorType.Failure);

    public static readonly Error InvalidShipMethod = new(
        "ShopeeOrder.InvalidShipMethod",
        "Ship method must be pickup or dropoff, with pickup requiring an address and time slot",
        ErrorType.Validation);

    public static readonly Error TrackingNumberFetchFailed = new(
        "ShopeeOrder.TrackingNumberFetchFailed",
        "Shopee did not return the tracking number",
        ErrorType.Failure);

    public static readonly Error TrackingNumberConflict = new(
        "ShopeeOrder.TrackingNumberConflict",
        "The Shopee tracking number is already used by an existing waybill",
        ErrorType.Conflict);

    public static readonly Error LabelFetchFailed = new(
        "ShopeeOrder.LabelFetchFailed",
        "Shopee did not return the shipping document",
        ErrorType.Failure);

    public static readonly Error ProductNotFoundForItem = new(
        "ShopeeOrder.ProductNotFoundForItem",
        "A linked product for this order no longer exists",
        ErrorType.Conflict);

    public static readonly Error CancellationRequested = new(
        "ShopeeOrder.CancellationRequested",
        "The buyer has requested cancellation on Shopee; resolve it there before arranging shipment",
        ErrorType.Conflict);
}
