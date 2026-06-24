using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Events;

namespace Wrapsfer.Domain.Entities;

public sealed class PurchaseOrder : AuditableEntity
{
    public const int PoNumberMaxLength = 100;
    public const int RejectionReasonMaxLength = 1000;

    private PurchaseOrder()
    {
    }

    public string TenantId { get; private set; } = default!;
    public string PoNumber { get; private set; } = default!;
    public Guid ProductId { get; private set; }
    public string ProductBarcode { get; private set; } = default!;
    public string ProductName { get; private set; } = default!;
    public int Quantity { get; private set; }
    public PurchaseOrderStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTime? ReceivedAt { get; private set; }
    public DateTime? RejectedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public static Result<PurchaseOrder> Create(
        string tenantId,
        string poNumber,
        Guid productId,
        string productBarcode,
        string productName,
        int quantity)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<PurchaseOrder>.Failure(PurchaseOrderErrors.InvalidTenantId);
        }

        if (string.IsNullOrWhiteSpace(poNumber))
        {
            return Result<PurchaseOrder>.Failure(PurchaseOrderErrors.InvalidPoNumber);
        }

        if (poNumber.Length > PoNumberMaxLength)
        {
            return Result<PurchaseOrder>.Failure(PurchaseOrderErrors.PoNumberTooLong);
        }

        if (productId == Guid.Empty)
        {
            return Result<PurchaseOrder>.Failure(PurchaseOrderErrors.InvalidProductId);
        }

        if (string.IsNullOrWhiteSpace(productBarcode))
        {
            return Result<PurchaseOrder>.Failure(PurchaseOrderErrors.InvalidBarcode);
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            return Result<PurchaseOrder>.Failure(PurchaseOrderErrors.InvalidProductName);
        }

        if (quantity <= 0)
        {
            return Result<PurchaseOrder>.Failure(PurchaseOrderErrors.InvalidQuantity);
        }

        PurchaseOrder purchaseOrder = new()
        {
            TenantId = tenantId,
            PoNumber = poNumber,
            ProductId = productId,
            ProductBarcode = productBarcode,
            ProductName = productName,
            Quantity = quantity,
            Status = PurchaseOrderStatus.Pending
        };

        purchaseOrder.AddDomainEvent(new PurchaseOrderCreatedEvent(
            purchaseOrder.Id,
            tenantId,
            poNumber,
            productId,
            productName,
            productBarcode,
            quantity,
            DateTime.UtcNow));

        return Result<PurchaseOrder>.Success(purchaseOrder);
    }

    public Result Receive()
    {
        if (Status != PurchaseOrderStatus.Pending)
        {
            return Result.Failure(PurchaseOrderErrors.InvalidStatusTransition);
        }

        Status = PurchaseOrderStatus.Received;
        ReceivedAt = DateTime.UtcNow;

        AddDomainEvent(new PurchaseOrderReceivedEvent(
            Id,
            TenantId,
            PoNumber,
            ProductId,
            ProductName,
            ProductBarcode,
            Quantity,
            DateTime.UtcNow));

        return Result.Success();
    }

    public Result Reject(string reason)
    {
        if (Status != PurchaseOrderStatus.Pending)
        {
            return Result.Failure(PurchaseOrderErrors.InvalidStatusTransition);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(PurchaseOrderErrors.RejectionReasonRequired);
        }

        string trimmedReason = reason.Length > RejectionReasonMaxLength
            ? reason[..RejectionReasonMaxLength]
            : reason;

        Status = PurchaseOrderStatus.Rejected;
        RejectionReason = trimmedReason;
        RejectedAt = DateTime.UtcNow;

        AddDomainEvent(new PurchaseOrderRejectedEvent(
            Id,
            TenantId,
            PoNumber,
            ProductId,
            ProductName,
            ProductBarcode,
            Quantity,
            trimmedReason,
            DateTime.UtcNow));

        return Result.Success();
    }

    public Result Update(string poNumber, int quantity)
    {
        if (Status != PurchaseOrderStatus.Pending)
        {
            return Result.Failure(PurchaseOrderErrors.NotPendingForEdit);
        }

        if (string.IsNullOrWhiteSpace(poNumber))
        {
            return Result.Failure(PurchaseOrderErrors.InvalidPoNumber);
        }

        if (poNumber.Length > PoNumberMaxLength)
        {
            return Result.Failure(PurchaseOrderErrors.PoNumberTooLong);
        }

        if (quantity <= 0)
        {
            return Result.Failure(PurchaseOrderErrors.InvalidQuantity);
        }

        PoNumber = poNumber;
        Quantity = quantity;

        return Result.Success();
    }

    public Result Delete()
    {
        if (Status != PurchaseOrderStatus.Pending)
        {
            return Result.Failure(PurchaseOrderErrors.NotPendingForDelete);
        }

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;

        return Result.Success();
    }
}
