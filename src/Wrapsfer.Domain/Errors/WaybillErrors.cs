using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Errors;

public static class WaybillErrors
{
    public static readonly Error NotFound = new(
        "Waybill.NotFound",
        "The waybill was not found",
        ErrorType.NotFound);

    public static readonly Error InvalidTenantId = new(
        "Waybill.InvalidTenantId",
        "Tenant ID is required",
        ErrorType.Validation);

    public static readonly Error InvalidWaybillNumber = new(
        "Waybill.InvalidWaybillNumber",
        "Waybill number is required",
        ErrorType.Validation);

    public static readonly Error WaybillNumberTooLong = new(
        "Waybill.WaybillNumberTooLong",
        "Waybill number must be 100 characters or fewer",
        ErrorType.Validation);

    public static readonly Error DuplicateWaybillNumber = new(
        "Waybill.DuplicateWaybillNumber",
        "A waybill with this number already exists for this tenant",
        ErrorType.Conflict);

    public static readonly Error InvalidProductId = new(
        "Waybill.InvalidProductId",
        "Product ID must not be empty",
        ErrorType.Validation);

    public static readonly Error InvalidBarcode = new(
        "Waybill.InvalidBarcode",
        "Product barcode is required",
        ErrorType.Validation);

    public static readonly Error InvalidQuantity = new(
        "Waybill.InvalidQuantity",
        "Quantity must be greater than zero",
        ErrorType.Validation);

    public static readonly Error ItemNotFound = new(
        "Waybill.ItemNotFound",
        "The waybill item was not found",
        ErrorType.NotFound);

    public static readonly Error InvalidStatusTransition = new(
        "Waybill.InvalidStatusTransition",
        "The requested status transition is not allowed from the current state",
        ErrorType.Conflict);

    public static readonly Error NotWaybillCreator = new(
        "Waybill.NotWaybillCreator",
        "Only the waybill creator can perform this action",
        ErrorType.Conflict);

    public static readonly Error CannotEditNonDraft = new(
        "Waybill.CannotEditNonDraft",
        "Waybill items can only be modified while the waybill is in Draft status",
        ErrorType.Conflict);

    public static readonly Error CancellationReasonRequired = new(
        "Waybill.CancellationReasonRequired",
        "A reason is required when cancelling a waybill",
        ErrorType.Validation);

    public static readonly Error CannotDeleteNonDraft = new(
        "Waybill.CannotDeleteNonDraft",
        "Only Draft waybills can be deleted",
        ErrorType.Conflict);

    public static readonly Error EmptyWaybill = new(
        "Waybill.EmptyWaybill",
        "A waybill must contain at least one item before it can be marked as packed",
        ErrorType.Validation);

    public static readonly Error CannotCancelAfterHandedOff = new(
        "Waybill.CannotCancelAfterHandedOff",
        "A waybill that has been handed off cannot be cancelled",
        ErrorType.Conflict);

    public static readonly Error CannotRestoreNonAutoCancelledDraft = new(
        "Waybill.CannotRestoreNonAutoCancelledDraft",
        "Only auto-cancelled stale draft waybills can be restored",
        ErrorType.Conflict);
}
