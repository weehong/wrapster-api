using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Events;

namespace Wrapsfer.Domain.Entities;

public sealed class Waybill : AuditableEntity
{
    private readonly List<WaybillItem> _items = [];

    private Waybill()
    {
    }

    public string TenantId { get; private set; } = default!;
    public DateOnly PackagingDate { get; private set; }
    public string WaybillNumber { get; private set; } = default!;
    public WaybillStatus Status { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime? PackedAt { get; private set; }
    public DateTime? HandedOffAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    public IReadOnlyCollection<WaybillItem> Items => _items.AsReadOnly();

    public static Result<Waybill> Create(string tenantId, DateOnly packagingDate, string waybillNumber)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<Waybill>.Failure(WaybillErrors.InvalidTenantId);
        }

        if (string.IsNullOrWhiteSpace(waybillNumber))
        {
            return Result<Waybill>.Failure(WaybillErrors.InvalidWaybillNumber);
        }

        if (waybillNumber.Length > 100)
        {
            return Result<Waybill>.Failure(WaybillErrors.WaybillNumberTooLong);
        }

        Waybill waybill = new()
        {
            TenantId = tenantId,
            PackagingDate = packagingDate,
            WaybillNumber = waybillNumber,
            Status = WaybillStatus.Draft
        };

        waybill.AddDomainEvent(new WaybillCreatedEvent(
            waybill.Id,
            tenantId,
            packagingDate,
            waybillNumber,
            DateTime.UtcNow));

        return Result<Waybill>.Success(waybill);
    }

    public Result UpdateWaybillNumber(string newWaybillNumber)
    {
        if (Status != WaybillStatus.Draft)
        {
            return Result.Failure(WaybillErrors.CannotEditNonDraft);
        }

        if (string.IsNullOrWhiteSpace(newWaybillNumber))
        {
            return Result.Failure(WaybillErrors.InvalidWaybillNumber);
        }

        if (newWaybillNumber.Length > 100)
        {
            return Result.Failure(WaybillErrors.WaybillNumberTooLong);
        }

        WaybillNumber = newWaybillNumber;
        return Result.Success();
    }

    public Result<WaybillItem> AddOrIncrementItem(Guid productId, string productBarcode, int quantity)
    {
        if (Status != WaybillStatus.Draft)
        {
            return Result<WaybillItem>.Failure(WaybillErrors.CannotEditNonDraft);
        }

        if (productId == Guid.Empty)
        {
            return Result<WaybillItem>.Failure(WaybillErrors.InvalidProductId);
        }

        if (string.IsNullOrWhiteSpace(productBarcode))
        {
            return Result<WaybillItem>.Failure(WaybillErrors.InvalidBarcode);
        }

        if (quantity <= 0)
        {
            return Result<WaybillItem>.Failure(WaybillErrors.InvalidQuantity);
        }

        WaybillItem? existing = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is not null)
        {
            existing.IncrementQuantity(quantity);

            AddDomainEvent(new WaybillItemAddedEvent(
                Id,
                existing.Id,
                productId,
                productBarcode,
                quantity,
                TenantId,
                DateTime.UtcNow));

            return Result<WaybillItem>.Success(existing);
        }

        Result<WaybillItem> itemResult = WaybillItem.Create(TenantId, Id, productId, productBarcode, quantity);
        if (itemResult.IsFailure)
        {
            return itemResult;
        }

        _items.Add(itemResult.Value);

        AddDomainEvent(new WaybillItemAddedEvent(
            Id,
            itemResult.Value.Id,
            productId,
            productBarcode,
            quantity,
            TenantId,
            DateTime.UtcNow));

        return itemResult;
    }

    public Result<QuantityChange> UpdateItemQuantity(Guid itemId, int newQuantity)
    {
        if (Status != WaybillStatus.Draft)
        {
            return Result<QuantityChange>.Failure(WaybillErrors.CannotEditNonDraft);
        }

        if (newQuantity <= 0)
        {
            return Result<QuantityChange>.Failure(WaybillErrors.InvalidQuantity);
        }

        WaybillItem? item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
        {
            return Result<QuantityChange>.Failure(WaybillErrors.ItemNotFound);
        }

        int delta = newQuantity - item.Quantity;
        Result setResult = item.SetQuantity(newQuantity);
        if (setResult.IsFailure)
        {
            return Result<QuantityChange>.Failure(setResult.Error);
        }

        return Result<QuantityChange>.Success(new QuantityChange(item.ProductId, delta));
    }

    public Result<WaybillItemRemoval> RemoveItem(Guid itemId)
    {
        if (Status != WaybillStatus.Draft)
        {
            return Result<WaybillItemRemoval>.Failure(WaybillErrors.CannotEditNonDraft);
        }

        WaybillItem? item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
        {
            return Result<WaybillItemRemoval>.Failure(WaybillErrors.ItemNotFound);
        }

        _items.Remove(item);
        return Result<WaybillItemRemoval>.Success(new WaybillItemRemoval(item.ProductId, item.Quantity));
    }

    public Result MarkPacked()
    {
        if (Status != WaybillStatus.Draft)
        {
            return Result.Failure(WaybillErrors.InvalidStatusTransition);
        }

        if (_items.Count == 0)
        {
            return Result.Failure(WaybillErrors.EmptyWaybill);
        }

        Status = WaybillStatus.Packed;
        PackedAt = DateTime.UtcNow;

        AddDomainEvent(new WaybillPackedEvent(Id, TenantId, DateTime.UtcNow));

        return Result.Success();
    }

    public Result MarkHandedOff(string actingUserId)
    {
        if (Status != WaybillStatus.Packed)
        {
            return Result.Failure(WaybillErrors.InvalidStatusTransition);
        }

        if (string.IsNullOrWhiteSpace(actingUserId) ||
            !string.Equals(actingUserId, CreatedBy, StringComparison.Ordinal))
        {
            return Result.Failure(WaybillErrors.NotWaybillCreator);
        }

        Status = WaybillStatus.HandedOff;
        HandedOffAt = DateTime.UtcNow;

        AddDomainEvent(new WaybillHandedOffEvent(Id, TenantId, actingUserId, DateTime.UtcNow));

        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(WaybillErrors.CancellationReasonRequired);
        }

        if (Status == WaybillStatus.HandedOff)
        {
            return Result.Failure(WaybillErrors.CannotCancelAfterHandedOff);
        }

        if (Status == WaybillStatus.Cancelled)
        {
            return Result.Failure(WaybillErrors.InvalidStatusTransition);
        }

        WaybillStatus previousStatus = Status;
        Status = WaybillStatus.Cancelled;
        CancellationReason = reason;
        CancelledAt = DateTime.UtcNow;

        AddDomainEvent(new WaybillCancelledEvent(Id, TenantId, previousStatus, reason, DateTime.UtcNow));

        return Result.Success();
    }
}
