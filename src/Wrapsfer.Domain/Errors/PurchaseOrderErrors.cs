using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Errors;

public static class PurchaseOrderErrors
{
    public static readonly Error NotFound = new(
        "PurchaseOrder.NotFound",
        "The purchase order was not found",
        ErrorType.NotFound);

    public static readonly Error InvalidTenantId = new(
        "PurchaseOrder.InvalidTenantId",
        "Tenant ID is required",
        ErrorType.Validation);

    public static readonly Error InvalidPoNumber = new(
        "PurchaseOrder.InvalidPoNumber",
        "Purchase order number is required",
        ErrorType.Validation);

    public static readonly Error PoNumberTooLong = new(
        "PurchaseOrder.PoNumberTooLong",
        "Purchase order number must be 100 characters or fewer",
        ErrorType.Validation);

    public static readonly Error DuplicatePoNumber = new(
        "PurchaseOrder.DuplicatePoNumber",
        "A purchase order with this number already exists for this tenant",
        ErrorType.Conflict);

    public static readonly Error InvalidProductId = new(
        "PurchaseOrder.InvalidProductId",
        "Product ID must not be empty",
        ErrorType.Validation);

    public static readonly Error InvalidBarcode = new(
        "PurchaseOrder.InvalidBarcode",
        "Product barcode is required",
        ErrorType.Validation);

    public static readonly Error InvalidProductName = new(
        "PurchaseOrder.InvalidProductName",
        "Product name is required",
        ErrorType.Validation);

    public static readonly Error InvalidQuantity = new(
        "PurchaseOrder.InvalidQuantity",
        "Quantity must be greater than zero",
        ErrorType.Validation);

    public static readonly Error ProductNotFound = new(
        "PurchaseOrder.ProductNotFound",
        "The product for this purchase order was not found",
        ErrorType.NotFound);

    public static readonly Error ProductInactive = new(
        "PurchaseOrder.ProductInactive",
        "A purchase order cannot be created for an inactive product",
        ErrorType.Validation);

    public static readonly Error ProductNotStockable = new(
        "PurchaseOrder.ProductNotStockable",
        "A purchase order cannot be created for a bundle product, which holds no stock of its own",
        ErrorType.Validation);

    public static readonly Error InvalidStatusTransition = new(
        "PurchaseOrder.InvalidStatusTransition",
        "The requested action is not allowed from the current status",
        ErrorType.Conflict);

    public static readonly Error RejectionReasonRequired = new(
        "PurchaseOrder.RejectionReasonRequired",
        "A reason is required when rejecting a purchase order",
        ErrorType.Validation);
}
