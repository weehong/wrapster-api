using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Entities;

public sealed class ShopeeOrder : AuditableEntity
{
    private const int OrderSnMaxLength = 64;
    private const int RegionMaxLength = 16;
    private const int NameMaxLength = 256;
    private const int PhoneMaxLength = 64;
    private const int AddressMaxLength = 1024;
    private const int CurrencyMaxLength = 8;
    private const int CarrierMaxLength = 128;
    private const int ShopeeStatusMaxLength = 32;
    private const int TrackingNumberMaxLength = 100;
    private const int ErrorMaxLength = 512;
    private const int LabelKeyMaxLength = 512;

    private readonly List<ShopeeOrderItem> _items = [];

    private ShopeeOrder()
    {
    }

    public string TenantId { get; private set; } = default!;
    public string OrderSn { get; private set; } = default!;
    public string? Region { get; private set; }
    public string ShopeeStatus { get; private set; } = default!;
    public string? BuyerUsername { get; private set; }
    public string? RecipientName { get; private set; }
    public string? RecipientPhone { get; private set; }
    public string? RecipientAddress { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string? Currency { get; private set; }
    public decimal? CodAmount { get; private set; }
    public string? ShippingCarrier { get; private set; }
    public DateTime? ShipByDate { get; private set; }
    public ShopeeOrderStatus Status { get; private set; }
    public string? TrackingNumber { get; private set; }
    public Guid? WaybillId { get; private set; }
    public DateTime? ShipmentArrangedAt { get; private set; }
    public string? ShipmentArrangedBy { get; private set; }
    public string? LabelStorageKey { get; private set; }
    public DateTime? LabelPrintedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }
    public string? LastShipError { get; private set; }

    public IReadOnlyCollection<ShopeeOrderItem> Items => _items.AsReadOnly();

    public bool HasUnresolvedItems => _items.Count == 0 || _items.Any(i => i.ProductId is null);

    public static Result<ShopeeOrder> Create(
        string tenantId,
        string orderSn,
        string? region,
        ShopeeOrderSnapshot snapshot,
        IReadOnlyList<ShopeeOrderItemSnapshot> items,
        DateTime syncedAt)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<ShopeeOrder>.Failure(ShopeeOrderErrors.InvalidTenantId);
        }

        if (string.IsNullOrWhiteSpace(orderSn) || orderSn.Length > OrderSnMaxLength)
        {
            return Result<ShopeeOrder>.Failure(ShopeeOrderErrors.InvalidOrderSn);
        }

        ShopeeOrder order = new()
        {
            TenantId = tenantId.Trim(),
            OrderSn = orderSn.Trim(),
            Region = Truncate(region, RegionMaxLength)
        };

        Result applyResult = order.ApplyShopeeSnapshot(snapshot, items, syncedAt);
        if (applyResult.IsFailure)
        {
            return Result<ShopeeOrder>.Failure(applyResult.Error);
        }

        return Result<ShopeeOrder>.Success(order);
    }

    public Result ApplyShopeeSnapshot(
        ShopeeOrderSnapshot snapshot,
        IReadOnlyList<ShopeeOrderItemSnapshot> items,
        DateTime syncedAt)
    {
        if (string.IsNullOrWhiteSpace(snapshot.ShopeeStatus))
        {
            return Result.Failure(ShopeeOrderErrors.InvalidShopeeStatus);
        }

        if (items.Count == 0)
        {
            return Result.Failure(ShopeeOrderErrors.NoItems);
        }

        if (items.Any(i => i.Quantity <= 0))
        {
            return Result.Failure(ShopeeOrderErrors.InvalidQuantity);
        }

        ShopeeStatus = Truncate(snapshot.ShopeeStatus, ShopeeStatusMaxLength)!;
        BuyerUsername = Truncate(snapshot.BuyerUsername, NameMaxLength);
        RecipientName = Truncate(snapshot.RecipientName, NameMaxLength);
        RecipientPhone = Truncate(snapshot.RecipientPhone, PhoneMaxLength);
        RecipientAddress = Truncate(snapshot.RecipientAddress, AddressMaxLength);
        TotalAmount = snapshot.TotalAmount;
        Currency = Truncate(snapshot.Currency, CurrencyMaxLength);
        CodAmount = snapshot.CodAmount;
        ShippingCarrier = Truncate(snapshot.ShippingCarrier, CarrierMaxLength);
        ShipByDate = snapshot.ShipByDate;
        LastSyncedAt = syncedAt;

        _items.Clear();
        foreach (ShopeeOrderItemSnapshot item in items)
        {
            _items.Add(ShopeeOrderItem.FromSnapshot(TenantId, Id, item));
        }

        DeriveLinkStatus();
        return Result.Success();
    }

    public Result MarkShipmentArranged(string? arrangedBy, DateTime arrangedAt)
    {
        if (Status is not (ShopeeOrderStatus.ReadyToShip or ShopeeOrderStatus.ShipmentFailed))
        {
            return Result.Failure(ShopeeOrderErrors.NotReadyToShip);
        }

        if (HasUnresolvedItems)
        {
            return Result.Failure(ShopeeOrderErrors.ItemsNotLinked);
        }

        Status = ShopeeOrderStatus.AwaitingTracking;
        ShipmentArrangedAt = arrangedAt;
        ShipmentArrangedBy = Truncate(arrangedBy, NameMaxLength);
        LastShipError = null;
        return Result.Success();
    }

    public Result AssignTracking(string trackingNumber)
    {
        if (Status != ShopeeOrderStatus.AwaitingTracking)
        {
            return Result.Failure(ShopeeOrderErrors.NotAwaitingTracking);
        }

        if (string.IsNullOrWhiteSpace(trackingNumber) || trackingNumber.Length > TrackingNumberMaxLength)
        {
            return Result.Failure(ShopeeOrderErrors.InvalidTrackingNumber);
        }

        TrackingNumber = trackingNumber.Trim();
        return Result.Success();
    }

    public Result LinkWaybill(Guid waybillId)
    {
        if (Status != ShopeeOrderStatus.AwaitingTracking)
        {
            return Result.Failure(ShopeeOrderErrors.NotAwaitingTracking);
        }

        if (string.IsNullOrWhiteSpace(TrackingNumber))
        {
            return Result.Failure(ShopeeOrderErrors.TrackingNotAssigned);
        }

        if (waybillId == Guid.Empty)
        {
            return Result.Failure(ShopeeOrderErrors.InvalidWaybillId);
        }

        WaybillId = waybillId;
        Status = ShopeeOrderStatus.Shipped;
        return Result.Success();
    }

    public Result MarkShipmentFailed(string error, DateTime failedAt)
    {
        if (Status is not (ShopeeOrderStatus.ReadyToShip or ShopeeOrderStatus.AwaitingTracking))
        {
            return Result.Failure(ShopeeOrderErrors.CannotFailShipment);
        }

        if (string.IsNullOrWhiteSpace(error))
        {
            return Result.Failure(ShopeeOrderErrors.InvalidShipError);
        }

        Status = ShopeeOrderStatus.ShipmentFailed;
        LastShipError = Truncate(error, ErrorMaxLength);
        return Result.Success();
    }

    public Result MarkCancelled(DateTime cancelledAt)
    {
        if (Status == ShopeeOrderStatus.Cancelled)
        {
            return Result.Failure(ShopeeOrderErrors.AlreadyCancelled);
        }

        Status = ShopeeOrderStatus.Cancelled;
        CancelledAt = cancelledAt;
        return Result.Success();
    }

    public Result MarkLabelStored(string objectKey)
    {
        if (Status != ShopeeOrderStatus.Shipped)
        {
            return Result.Failure(ShopeeOrderErrors.NotShipped);
        }

        if (string.IsNullOrWhiteSpace(objectKey) || objectKey.Length > LabelKeyMaxLength)
        {
            return Result.Failure(ShopeeOrderErrors.InvalidLabelObjectKey);
        }

        LabelStorageKey = objectKey.Trim();
        return Result.Success();
    }

    public Result MarkLabelPrinted(DateTime printedAt)
    {
        if (Status != ShopeeOrderStatus.Shipped)
        {
            return Result.Failure(ShopeeOrderErrors.NotShipped);
        }

        LabelPrintedAt ??= printedAt;
        return Result.Success();
    }

    private void DeriveLinkStatus()
    {
        if (Status is not (ShopeeOrderStatus.NeedsLinking or ShopeeOrderStatus.ReadyToShip))
        {
            return;
        }

        Status = HasUnresolvedItems ? ShopeeOrderStatus.NeedsLinking : ShopeeOrderStatus.ReadyToShip;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
