# Shopee Order → Waybill Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sync Shopee orders into Wrapsfer via webhooks + reconciliation polling, let users arrange shipment from Wrapsfer, auto-create the Wrapsfer `Waybill` when the tracking number arrives, serve/persist the AWB label PDF, and sync cancellations back onto the linked waybill.

**Architecture:** New `ShopeeOrder` aggregate (+ `ShopeeOrderItem`, `ShopeeWebhookEvent`) in the existing Clean Architecture/CQRS stack. Shopee pushes land on an anonymous signature-verified endpoint that persists events; a background dispatcher processes them via an ingestion service that upserts orders and resolves items against `ShopeeProductLink`s. A user-triggered ship command calls Shopee's logistics APIs; a completion service creates the `Waybill` (reusing `StockReservationService`). An hourly reconciliation job heals missed pushes. Frontend is a new `shopee-orders` partner-portal feature mirroring `shopee-products`.

**Tech Stack:** .NET 10 / EF Core + PostgreSQL / MediatR + FluentValidation / xUnit + Moq + FluentAssertions (backend); TanStack Start + React 19 + Mantine v9 + TanStack Query + react-intl + Vitest (frontend).

**Spec:** `api/docs/superpowers/specs/2026-07-16-shopee-order-waybill-sync-design.md`

## Global Constraints

- Backend repo: `api/` (branch `shopee`). Frontend repo: `partner/` (separate git repo).
- **Never use `var`** — explicit types everywhere. One type per file. Max 1 consecutive blank line. `TreatWarningsAsErrors` is on.
- Every repository method reading/mutating tenant data MUST filter by `tenantId` (exceptions: methods documented as system-level for background jobs only, following `IShopeeShopConnectionRepository.ListRequiringRefreshAsync` precedent).
- Domain factory methods/mutators that can fail return `Result`/`Result<T>`; all error constants live in `Errors` classes.
- `xmin` concurrency token on entities subject to concurrent updates.
- Frontend: kebab-case files, one component per file, no cross-feature imports, all user-visible strings through react-intl, Drawer for create/edit flows, data fetched in route loaders.
- Backend build/test: `make build`, `make test`, or `dotnet test --no-restore --filter "FullyQualifiedName~<TestClass>"` from `api/`.
- Frontend: `pnpm typecheck`, `pnpm test`, `pnpm check`, `pnpm extract` from `partner/`.
- One deliberate deviation from the spec: `ShopeeOrderItem` stores `ProductId` only (no `ProductBarcode` column). The barcode is resolved fresh from `Product` at waybill-creation time, which avoids a stale-barcode copy. Everything else follows the spec.

---

### Task 1: Domain — `ShopeeOrder` aggregate, `ShopeeOrderItem`, status enum, errors

**Files:**
- Create: `api/src/Wrapsfer.Domain/Enums/ShopeeOrderStatus.cs`
- Create: `api/src/Wrapsfer.Domain/Entities/ShopeeOrderSnapshot.cs`
- Create: `api/src/Wrapsfer.Domain/Entities/ShopeeOrderItemSnapshot.cs`
- Create: `api/src/Wrapsfer.Domain/Entities/ShopeeOrderItem.cs`
- Create: `api/src/Wrapsfer.Domain/Entities/ShopeeOrder.cs`
- Create: `api/src/Wrapsfer.Domain/Errors/ShopeeOrderErrors.cs`
- Test: `api/tests/Wrapsfer.Domain.Tests/Entities/ShopeeOrderTests.cs`

**Interfaces:**
- Consumes: `AuditableEntity`, `BaseEntity`, `Result<T>`, `Error`/`ErrorType` (existing).
- Produces (later tasks rely on these exact members):
  - `ShopeeOrderStatus { NeedsLinking=0, ReadyToShip=1, AwaitingTracking=2, Shipped=3, Cancelled=4, ShipmentFailed=5 }`
  - `record ShopeeOrderSnapshot(string ShopeeStatus, string? BuyerUsername, string? RecipientName, string? RecipientPhone, string? RecipientAddress, decimal TotalAmount, string? Currency, decimal? CodAmount, string? ShippingCarrier, DateTime? ShipByDate)`
  - `record ShopeeOrderItemSnapshot(long ShopeeItemId, long ShopeeModelId, string? ItemName, string? ModelName, string? ItemSku, int Quantity, Guid? ProductId)`
  - `ShopeeOrder.Create(string tenantId, string orderSn, string? region, ShopeeOrderSnapshot snapshot, IReadOnlyList<ShopeeOrderItemSnapshot> items, DateTime syncedAt) : Result<ShopeeOrder>`
  - `ApplyShopeeSnapshot(ShopeeOrderSnapshot, IReadOnlyList<ShopeeOrderItemSnapshot>, DateTime syncedAt) : Result`
  - `MarkShipmentArranged(string? arrangedBy, DateTime arrangedAt) : Result` (from ReadyToShip/ShipmentFailed)
  - `AssignTracking(string trackingNumber) : Result` (from AwaitingTracking)
  - `LinkWaybill(Guid waybillId) : Result` (AwaitingTracking + tracking assigned → Shipped)
  - `MarkShipmentFailed(string error, DateTime failedAt) : Result` (from ReadyToShip/AwaitingTracking)
  - `MarkCancelled(DateTime cancelledAt) : Result` (any status except Cancelled)
  - `MarkLabelStored(string objectKey) : Result`, `MarkLabelPrinted(DateTime printedAt) : Result` (Shipped only; printed is set-once)
  - `bool HasUnresolvedItems` (any item with null `ProductId`)

- [ ] **Step 1: Write the failing domain tests**

`api/tests/Wrapsfer.Domain.Tests/Entities/ShopeeOrderTests.cs`:

```csharp
using FluentAssertions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Tests.Entities;

public sealed class ShopeeOrderTests
{
    private static readonly DateTime Now = new(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);

    private static ShopeeOrderSnapshot Snapshot(string status = "READY_TO_SHIP") =>
        new(status, "buyer1", "Jane", "+60123456789", "1 Jalan Test, KL",
            59.90m, "MYR", null, "SPX Express", Now.AddDays(2));

    private static ShopeeOrderItemSnapshot Item(Guid? productId, long itemId = 111, int quantity = 2) =>
        new(itemId, 0, "Item name", null, "SKU-1", quantity, productId);

    private static ShopeeOrder CreateOrder(params ShopeeOrderItemSnapshot[] items)
    {
        Result<ShopeeOrder> result = ShopeeOrder.Create(
            "tenant-a", "260716ABC123", "MY", Snapshot(), items, Now);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public void Create_WithAllItemsLinked_StartsReadyToShip()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        order.Status.Should().Be(ShopeeOrderStatus.ReadyToShip);
        order.HasUnresolvedItems.Should().BeFalse();
        order.Items.Should().HaveCount(1);
    }

    [Fact]
    public void Create_WithUnlinkedItem_StartsNeedsLinking()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()), Item(null, itemId: 222));
        order.Status.Should().Be(ShopeeOrderStatus.NeedsLinking);
        order.HasUnresolvedItems.Should().BeTrue();
    }

    [Fact]
    public void Create_WithoutItems_Fails()
    {
        Result<ShopeeOrder> result = ShopeeOrder.Create(
            "tenant-a", "260716ABC123", "MY", Snapshot(), [], Now);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.NoItems);
    }

    [Fact]
    public void ApplyShopeeSnapshot_ResolvingAllItems_MovesToReadyToShip()
    {
        ShopeeOrder order = CreateOrder(Item(null));
        Result result = order.ApplyShopeeSnapshot(Snapshot(), [Item(Guid.NewGuid())], Now.AddMinutes(5));
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.ReadyToShip);
        order.LastSyncedAt.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public void ApplyShopeeSnapshot_DoesNotDowngradeShippedOrder()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        order.MarkShipmentArranged("user1", Now);
        order.AssignTracking("MYTRACK123");
        order.LinkWaybill(Guid.NewGuid());
        order.ApplyShopeeSnapshot(Snapshot("SHIPPED"), [Item(null)], Now.AddHours(1));
        order.Status.Should().Be(ShopeeOrderStatus.Shipped);
    }

    [Fact]
    public void MarkShipmentArranged_FromReadyToShip_MovesToAwaitingTracking()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        Result result = order.MarkShipmentArranged("user1", Now);
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.AwaitingTracking);
        order.ShipmentArrangedBy.Should().Be("user1");
        order.ShipmentArrangedAt.Should().Be(Now);
        order.LastShipError.Should().BeNull();
    }

    [Fact]
    public void MarkShipmentArranged_FromNeedsLinking_Fails()
    {
        ShopeeOrder order = CreateOrder(Item(null));
        Result result = order.MarkShipmentArranged("user1", Now);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.NotReadyToShip);
    }

    [Fact]
    public void MarkShipmentFailed_ThenArrange_IsRetryable()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        order.MarkShipmentArranged("user1", Now);
        Result failResult = order.MarkShipmentFailed("courier rejected", Now);
        failResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.ShipmentFailed);
        order.LastShipError.Should().Be("courier rejected");
        Result retryResult = order.MarkShipmentArranged("user1", Now.AddMinutes(1));
        retryResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.AwaitingTracking);
    }

    [Fact]
    public void AssignTracking_ThenLinkWaybill_MovesToShipped()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        order.MarkShipmentArranged("user1", Now);
        Result trackResult = order.AssignTracking("MYTRACK123");
        trackResult.IsSuccess.Should().BeTrue();
        Guid waybillId = Guid.NewGuid();
        Result linkResult = order.LinkWaybill(waybillId);
        linkResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.Shipped);
        order.TrackingNumber.Should().Be("MYTRACK123");
        order.WaybillId.Should().Be(waybillId);
    }

    [Fact]
    public void LinkWaybill_WithoutTracking_Fails()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        order.MarkShipmentArranged("user1", Now);
        Result result = order.LinkWaybill(Guid.NewGuid());
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.TrackingNotAssigned);
    }

    [Fact]
    public void AssignTracking_WhenNotAwaitingTracking_Fails()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        Result result = order.AssignTracking("MYTRACK123");
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.NotAwaitingTracking);
    }

    [Fact]
    public void MarkCancelled_FromAnyActiveStatus_Succeeds_AndIsTerminal()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        Result first = order.MarkCancelled(Now);
        first.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.Cancelled);
        order.CancelledAt.Should().Be(Now);
        Result second = order.MarkCancelled(Now.AddMinutes(1));
        second.IsFailure.Should().BeTrue();
        second.Error.Should().Be(ShopeeOrderErrors.AlreadyCancelled);
    }

    [Fact]
    public void MarkLabelPrinted_IsSetOnce()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        order.MarkShipmentArranged("user1", Now);
        order.AssignTracking("MYTRACK123");
        order.LinkWaybill(Guid.NewGuid());
        order.MarkLabelStored("shopee-labels/tenant-a/260716ABC123.pdf").IsSuccess.Should().BeTrue();
        order.MarkLabelPrinted(Now).IsSuccess.Should().BeTrue();
        order.MarkLabelPrinted(Now.AddHours(1)).IsSuccess.Should().BeTrue();
        order.LabelPrintedAt.Should().Be(Now);
    }

    [Fact]
    public void MarkLabelStored_WhenNotShipped_Fails()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        Result result = order.MarkLabelStored("key");
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.NotShipped);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run from `api/`: `dotnet test --filter "FullyQualifiedName~ShopeeOrderTests"`
Expected: compilation FAILURE — `ShopeeOrder`, `ShopeeOrderStatus`, etc. do not exist.

- [ ] **Step 3: Implement the domain types**

`api/src/Wrapsfer.Domain/Enums/ShopeeOrderStatus.cs`:

```csharp
namespace Wrapsfer.Domain.Enums;

public enum ShopeeOrderStatus
{
    NeedsLinking = 0,
    ReadyToShip = 1,
    AwaitingTracking = 2,
    Shipped = 3,
    Cancelled = 4,
    ShipmentFailed = 5
}
```

`api/src/Wrapsfer.Domain/Entities/ShopeeOrderSnapshot.cs`:

```csharp
namespace Wrapsfer.Domain.Entities;

/// <summary>Scalar fields copied from Shopee's get_order_detail on every sync.</summary>
public sealed record ShopeeOrderSnapshot(
    string ShopeeStatus,
    string? BuyerUsername,
    string? RecipientName,
    string? RecipientPhone,
    string? RecipientAddress,
    decimal TotalAmount,
    string? Currency,
    decimal? CodAmount,
    string? ShippingCarrier,
    DateTime? ShipByDate);
```

`api/src/Wrapsfer.Domain/Entities/ShopeeOrderItemSnapshot.cs`:

```csharp
namespace Wrapsfer.Domain.Entities;

/// <summary>
/// One Shopee order line plus its resolution against ShopeeProductLinks.
/// A null <see cref="ProductId"/> means the Shopee item/model is not linked
/// to a Wrapsfer product, which blocks shipment (NeedsLinking).
/// </summary>
public sealed record ShopeeOrderItemSnapshot(
    long ShopeeItemId,
    long ShopeeModelId,
    string? ItemName,
    string? ModelName,
    string? ItemSku,
    int Quantity,
    Guid? ProductId);
```

`api/src/Wrapsfer.Domain/Entities/ShopeeOrderItem.cs`:

```csharp
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Entities;

public sealed class ShopeeOrderItem : BaseEntity
{
    private const int NameMaxLength = 256;
    private const int SkuMaxLength = 128;

    private ShopeeOrderItem()
    {
    }

    public string TenantId { get; private set; } = default!;
    public Guid ShopeeOrderId { get; private set; }
    public long ShopeeItemId { get; private set; }
    public long ShopeeModelId { get; private set; }
    public string? ItemName { get; private set; }
    public string? ModelName { get; private set; }
    public string? ItemSku { get; private set; }
    public int Quantity { get; private set; }
    public Guid? ProductId { get; private set; }

    internal static ShopeeOrderItem FromSnapshot(
        string tenantId, Guid shopeeOrderId, ShopeeOrderItemSnapshot snapshot)
    {
        return new ShopeeOrderItem
        {
            TenantId = tenantId,
            ShopeeOrderId = shopeeOrderId,
            ShopeeItemId = snapshot.ShopeeItemId,
            ShopeeModelId = snapshot.ShopeeModelId,
            ItemName = Truncate(snapshot.ItemName, NameMaxLength),
            ModelName = Truncate(snapshot.ModelName, NameMaxLength),
            ItemSku = Truncate(snapshot.ItemSku, SkuMaxLength),
            Quantity = snapshot.Quantity,
            ProductId = snapshot.ProductId
        };
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
```

`api/src/Wrapsfer.Domain/Errors/ShopeeOrderErrors.cs`:

```csharp
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
```

`api/src/Wrapsfer.Domain/Entities/ShopeeOrder.cs`:

```csharp
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
            return Result.Failure(ShopeeOrderErrors.NotAwaitingTracking);
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ShopeeOrderTests"`
Expected: all tests PASS.

- [ ] **Step 5: Commit**

```bash
cd api
git add src/Wrapsfer.Domain tests/Wrapsfer.Domain.Tests/Entities/ShopeeOrderTests.cs
git commit -m "feat(shopee): add ShopeeOrder aggregate with fulfillment state machine"
```

---

### Task 2: Domain — `ShopeeWebhookEvent` entity

**Files:**
- Create: `api/src/Wrapsfer.Domain/Enums/ShopeeWebhookEventStatus.cs`
- Create: `api/src/Wrapsfer.Domain/Entities/ShopeeWebhookEvent.cs`
- Create: `api/src/Wrapsfer.Domain/Errors/ShopeeWebhookEventErrors.cs`
- Test: `api/tests/Wrapsfer.Domain.Tests/Entities/ShopeeWebhookEventTests.cs`

**Interfaces:**
- Produces:
  - `ShopeeWebhookEventStatus { Pending=0, Processed=1, Failed=2, Ignored=3 }`
  - `ShopeeWebhookEvent.Create(long shopId, int code, string messageKey, string payload, DateTime receivedAt) : Result<ShopeeWebhookEvent>`
  - `MarkProcessed(DateTime processedAt)`, `MarkFailed(string error, DateTime attemptedAt, int maxAttempts)` (backoff 1min·2^n capped at 1h; after `maxAttempts` the status stays `Failed` with `NextAttemptAt = null` so it is never retried), `MarkIgnored()`
  - Properties: `ShopId`, `Code`, `MessageKey`, `Payload`, `Status`, `ReceivedAtUtc`, `ProcessedAtUtc`, `AttemptCount`, `NextAttemptAt`, `Error`

- [ ] **Step 1: Write the failing tests**

`api/tests/Wrapsfer.Domain.Tests/Entities/ShopeeWebhookEventTests.cs`:

```csharp
using FluentAssertions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Domain.Tests.Entities;

public sealed class ShopeeWebhookEventTests
{
    private static readonly DateTime Now = new(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);

    private static ShopeeWebhookEvent CreateEvent()
    {
        Result<ShopeeWebhookEvent> result = ShopeeWebhookEvent.Create(
            123456, 3, "abc123hash", "{\"code\":3}", Now);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public void Create_StartsPending()
    {
        ShopeeWebhookEvent evt = CreateEvent();
        evt.Status.Should().Be(ShopeeWebhookEventStatus.Pending);
        evt.AttemptCount.Should().Be(0);
        evt.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithBlankMessageKey_Fails()
    {
        Result<ShopeeWebhookEvent> result = ShopeeWebhookEvent.Create(123456, 3, " ", "{}", Now);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarkProcessed_SetsStatusAndTimestamp()
    {
        ShopeeWebhookEvent evt = CreateEvent();
        evt.MarkProcessed(Now.AddSeconds(5));
        evt.Status.Should().Be(ShopeeWebhookEventStatus.Processed);
        evt.ProcessedAtUtc.Should().Be(Now.AddSeconds(5));
    }

    [Fact]
    public void MarkFailed_BacksOffExponentially()
    {
        ShopeeWebhookEvent evt = CreateEvent();
        evt.MarkFailed("boom", Now, maxAttempts: 5);
        evt.Status.Should().Be(ShopeeWebhookEventStatus.Failed);
        evt.AttemptCount.Should().Be(1);
        evt.NextAttemptAt.Should().Be(Now.AddMinutes(1));
        evt.MarkFailed("boom", Now, maxAttempts: 5);
        evt.NextAttemptAt.Should().Be(Now.AddMinutes(2));
    }

    [Fact]
    public void MarkFailed_AfterMaxAttempts_StopsRetrying()
    {
        ShopeeWebhookEvent evt = CreateEvent();
        for (int i = 0; i < 5; i++)
        {
            evt.MarkFailed("boom", Now, maxAttempts: 5);
        }
        evt.AttemptCount.Should().Be(5);
        evt.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public void MarkIgnored_SetsStatus()
    {
        ShopeeWebhookEvent evt = CreateEvent();
        evt.MarkIgnored();
        evt.Status.Should().Be(ShopeeWebhookEventStatus.Ignored);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ShopeeWebhookEventTests"`
Expected: compilation FAILURE.

- [ ] **Step 3: Implement**

`api/src/Wrapsfer.Domain/Enums/ShopeeWebhookEventStatus.cs`:

```csharp
namespace Wrapsfer.Domain.Enums;

public enum ShopeeWebhookEventStatus
{
    Pending = 0,
    Processed = 1,
    Failed = 2,
    Ignored = 3
}
```

`api/src/Wrapsfer.Domain/Errors/ShopeeWebhookEventErrors.cs`:

```csharp
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Errors;

public static class ShopeeWebhookEventErrors
{
    public static readonly Error InvalidShopId = new(
        "ShopeeWebhookEvent.InvalidShopId", "Shop ID must be a positive number", ErrorType.Validation);

    public static readonly Error InvalidMessageKey = new(
        "ShopeeWebhookEvent.InvalidMessageKey", "Message key is required", ErrorType.Validation);

    public static readonly Error InvalidPayload = new(
        "ShopeeWebhookEvent.InvalidPayload", "Payload is required", ErrorType.Validation);

    public static readonly Error InvalidSignature = new(
        "ShopeeWebhookEvent.InvalidSignature", "The push signature is invalid", ErrorType.Validation);

    public static readonly Error MalformedPushBody = new(
        "ShopeeWebhookEvent.MalformedPushBody",
        "The push body is not valid Shopee push JSON",
        ErrorType.Validation);
}
```

`api/src/Wrapsfer.Domain/Entities/ShopeeWebhookEvent.cs`:

```csharp
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Entities;

/// <summary>
/// A raw Shopee push message persisted for idempotent, out-of-band processing.
/// Deduplicated by <see cref="MessageKey"/> (SHA-256 of the raw body, unique index),
/// mirroring the StripeWebhookEvent idempotency pattern.
/// </summary>
public sealed class ShopeeWebhookEvent : BaseEntity
{
    private const int MessageKeyMaxLength = 64;
    private const int ErrorMaxLength = 512;
    private static readonly TimeSpan s_initialBackoff = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan s_maxBackoff = TimeSpan.FromHours(1);

    private ShopeeWebhookEvent()
    {
    }

    public long ShopId { get; private set; }
    public int Code { get; private set; }
    public string MessageKey { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public ShopeeWebhookEventStatus Status { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public string? Error { get; private set; }

    public static Result<ShopeeWebhookEvent> Create(
        long shopId, int code, string messageKey, string payload, DateTime receivedAt)
    {
        if (shopId <= 0)
        {
            return Result<ShopeeWebhookEvent>.Failure(ShopeeWebhookEventErrors.InvalidShopId);
        }

        if (string.IsNullOrWhiteSpace(messageKey) || messageKey.Length > MessageKeyMaxLength)
        {
            return Result<ShopeeWebhookEvent>.Failure(ShopeeWebhookEventErrors.InvalidMessageKey);
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            return Result<ShopeeWebhookEvent>.Failure(ShopeeWebhookEventErrors.InvalidPayload);
        }

        ShopeeWebhookEvent webhookEvent = new()
        {
            ShopId = shopId,
            Code = code,
            MessageKey = messageKey.Trim(),
            Payload = payload,
            Status = ShopeeWebhookEventStatus.Pending,
            ReceivedAtUtc = receivedAt
        };

        return Result<ShopeeWebhookEvent>.Success(webhookEvent);
    }

    public void MarkProcessed(DateTime processedAt)
    {
        Status = ShopeeWebhookEventStatus.Processed;
        ProcessedAtUtc = processedAt;
        Error = null;
        NextAttemptAt = null;
    }

    public void MarkFailed(string error, DateTime attemptedAt, int maxAttempts)
    {
        Status = ShopeeWebhookEventStatus.Failed;
        AttemptCount++;
        Error = Truncate(error, ErrorMaxLength);

        if (AttemptCount >= maxAttempts)
        {
            NextAttemptAt = null;
            return;
        }

        double multiplier = Math.Pow(2, AttemptCount - 1);
        double minutes = Math.Min(s_initialBackoff.TotalMinutes * multiplier, s_maxBackoff.TotalMinutes);
        NextAttemptAt = attemptedAt.AddMinutes(minutes);
    }

    public void MarkIgnored()
    {
        Status = ShopeeWebhookEventStatus.Ignored;
        NextAttemptAt = null;
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ShopeeWebhookEventTests"`
Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Wrapsfer.Domain tests/Wrapsfer.Domain.Tests/Entities/ShopeeWebhookEventTests.cs
git commit -m "feat(shopee): add ShopeeWebhookEvent entity for idempotent push processing"
```

---

### Task 3: Persistence — repositories, EF configurations, migration

**Files:**
- Create: `api/src/Wrapsfer.Domain/Repositories/IShopeeOrderRepository.cs`
- Create: `api/src/Wrapsfer.Domain/Repositories/IShopeeWebhookEventRepository.cs`
- Modify: `api/src/Wrapsfer.Domain/Repositories/IShopeeShopConnectionRepository.cs` (add `GetByShopIdAsync`, `ListAllAsync`)
- Create: `api/src/Wrapsfer.Infrastructure/Persistence/Configurations/ShopeeOrderConfiguration.cs`
- Create: `api/src/Wrapsfer.Infrastructure/Persistence/Configurations/ShopeeOrderItemConfiguration.cs`
- Create: `api/src/Wrapsfer.Infrastructure/Persistence/Configurations/ShopeeWebhookEventConfiguration.cs`
- Create: `api/src/Wrapsfer.Infrastructure/Persistence/Repositories/ShopeeOrderRepository.cs`
- Create: `api/src/Wrapsfer.Infrastructure/Persistence/Repositories/ShopeeWebhookEventRepository.cs`
- Modify: `api/src/Wrapsfer.Infrastructure/Persistence/Repositories/ShopeeShopConnectionRepository.cs` (implement the two new methods)
- Modify: `api/src/Wrapsfer.Infrastructure/DependencyInjection.cs` (register the two repositories next to the existing `IShopeeProductLinkRepository` registration at ~line 83)
- Create: migration `AddShopeeOrders` (generated)

**Interfaces:**
- Consumes: Task 1 & 2 entities.
- Produces:
  - `IShopeeOrderRepository`:
    - `Task<ShopeeOrder?> GetByIdAsync(Guid id, string tenantId, CancellationToken ct = default)` (includes Items)
    - `Task<ShopeeOrder?> GetByOrderSnAsync(string orderSn, string tenantId, CancellationToken ct = default)` (includes Items)
    - `Task<(IReadOnlyList<ShopeeOrder> Items, int TotalCount)> ListAsync(string tenantId, ShopeeOrderStatus? status, string? search, int page, int pageSize, CancellationToken ct = default)` (includes Items; search matches `OrderSn` or `TrackingNumber`, case-insensitive contains; ordered by `CreatedAt` desc)
    - `Task<IReadOnlyList<ShopeeOrder>> ListAwaitingTrackingAsync(DateTime arrangedBefore, int batchSize, CancellationToken ct = default)` — system-level, spans tenants, background-job-only (document like `ListRequiringRefreshAsync`)
    - `void Add(ShopeeOrder order)`
  - `IShopeeWebhookEventRepository`:
    - `Task<bool> ExistsByMessageKeyAsync(string messageKey, CancellationToken ct = default)`
    - `Task<IReadOnlyList<ShopeeWebhookEvent>> ListPendingAsync(DateTime now, int batchSize, CancellationToken ct = default)` — `Status == Pending` OR (`Status == Failed` AND `NextAttemptAt != null` AND `NextAttemptAt <= now`), ordered by `ReceivedAtUtc`
    - `void Add(ShopeeWebhookEvent webhookEvent)`
  - `IShopeeShopConnectionRepository` additions:
    - `Task<ShopeeShopConnection?> GetByShopIdAsync(long shopId, CancellationToken ct = default)` — system-level (push routing), no tenant filter, background/webhook-only
    - `Task<IReadOnlyList<ShopeeShopConnection>> ListAllAsync(CancellationToken ct = default)` — system-level (reconciliation)

- [ ] **Step 1: Write the repository interfaces**

`IShopeeOrderRepository.cs`:

```csharp
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Domain.Repositories;

public interface IShopeeOrderRepository
{
    Task<ShopeeOrder?> GetByIdAsync(Guid id, string tenantId, CancellationToken cancellationToken = default);

    Task<ShopeeOrder?> GetByOrderSnAsync(string orderSn, string tenantId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ShopeeOrder> Items, int TotalCount)> ListAsync(
        string tenantId,
        ShopeeOrderStatus? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists orders stuck in AwaitingTracking whose shipment was arranged before
    /// <paramref name="arrangedBefore"/>. Intentionally spans all tenants: it exists solely
    /// for the reconciliation background job's tracking-number re-poll, never for
    /// user-driven request handling.
    /// </summary>
    Task<IReadOnlyList<ShopeeOrder>> ListAwaitingTrackingAsync(
        DateTime arrangedBefore, int batchSize, CancellationToken cancellationToken = default);

    void Add(ShopeeOrder order);
}
```

`IShopeeWebhookEventRepository.cs`:

```csharp
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Domain.Repositories;

public interface IShopeeWebhookEventRepository
{
    Task<bool> ExistsByMessageKeyAsync(string messageKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists events ready for processing: Pending, or Failed with a due retry. Spans all
    /// shops/tenants by design — used only by the webhook dispatch background job.
    /// </summary>
    Task<IReadOnlyList<ShopeeWebhookEvent>> ListPendingAsync(
        DateTime now, int batchSize, CancellationToken cancellationToken = default);

    void Add(ShopeeWebhookEvent webhookEvent);
}
```

Add to `IShopeeShopConnectionRepository.cs` (below `ListRequiringRefreshAsync`):

```csharp
    /// <summary>
    /// Resolves the connection owning a Shopee shop ID. Spans all tenants by design —
    /// used only to route incoming Shopee push messages to a tenant.
    /// </summary>
    Task<ShopeeShopConnection?> GetByShopIdAsync(long shopId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every connection. Spans all tenants by design — used only by the
    /// order reconciliation background job.
    /// </summary>
    Task<IReadOnlyList<ShopeeShopConnection>> ListAllAsync(CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Write the EF configurations**

`ShopeeOrderConfiguration.cs` (mirror `WaybillConfiguration.cs` for the items navigation — open it and copy its `HasMany`/field-access idiom exactly):

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class ShopeeOrderConfiguration : IEntityTypeConfiguration<ShopeeOrder>
{
    public void Configure(EntityTypeBuilder<ShopeeOrder> builder)
    {
        builder.ToTable("ShopeeOrders");
        builder.HasKey(o => o.Id);

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Property(o => o.TenantId).HasMaxLength(63).IsRequired();
        builder.Property(o => o.OrderSn).HasMaxLength(64).IsRequired();
        builder.Property(o => o.Region).HasMaxLength(16);
        builder.Property(o => o.ShopeeStatus).HasMaxLength(32).IsRequired();
        builder.Property(o => o.BuyerUsername).HasMaxLength(256);
        builder.Property(o => o.RecipientName).HasMaxLength(256);
        builder.Property(o => o.RecipientPhone).HasMaxLength(64);
        builder.Property(o => o.RecipientAddress).HasMaxLength(1024);
        builder.Property(o => o.TotalAmount).HasPrecision(18, 2);
        builder.Property(o => o.Currency).HasMaxLength(8);
        builder.Property(o => o.CodAmount).HasPrecision(18, 2);
        builder.Property(o => o.ShippingCarrier).HasMaxLength(128);
        builder.Property(o => o.Status).IsRequired();
        builder.Property(o => o.TrackingNumber).HasMaxLength(100);
        builder.Property(o => o.ShipmentArrangedBy).HasMaxLength(256);
        builder.Property(o => o.LabelStorageKey).HasMaxLength(512);
        builder.Property(o => o.LastShipError).HasMaxLength(512);

        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.UpdatedAt);
        builder.Property(o => o.CreatedBy).HasMaxLength(256);
        builder.Property(o => o.UpdatedBy).HasMaxLength(256);

        builder.HasOne<Waybill>()
            .WithMany()
            .HasForeignKey(o => o.WaybillId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.ShopeeOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(o => new { o.TenantId, o.OrderSn }).IsUnique();
        builder.HasIndex(o => new { o.TenantId, o.Status });
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.ShipmentArrangedAt);
    }
}
```

`ShopeeOrderItemConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class ShopeeOrderItemConfiguration : IEntityTypeConfiguration<ShopeeOrderItem>
{
    public void Configure(EntityTypeBuilder<ShopeeOrderItem> builder)
    {
        builder.ToTable("ShopeeOrderItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.TenantId).HasMaxLength(63).IsRequired();
        builder.Property(i => i.ShopeeOrderId).IsRequired();
        builder.Property(i => i.ShopeeItemId).IsRequired();
        builder.Property(i => i.ShopeeModelId).IsRequired();
        builder.Property(i => i.ItemName).HasMaxLength(256);
        builder.Property(i => i.ModelName).HasMaxLength(256);
        builder.Property(i => i.ItemSku).HasMaxLength(128);
        builder.Property(i => i.Quantity).IsRequired();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.ShopeeOrderId);
        builder.HasIndex(i => new { i.TenantId, i.ShopeeItemId, i.ShopeeModelId });
    }
}
```

`ShopeeWebhookEventConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Infrastructure.Persistence.Configurations;

public sealed class ShopeeWebhookEventConfiguration : IEntityTypeConfiguration<ShopeeWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ShopeeWebhookEvent> builder)
    {
        builder.ToTable("ShopeeWebhookEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ShopId).IsRequired();
        builder.Property(e => e.Code).IsRequired();
        builder.Property(e => e.MessageKey).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Payload).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.ReceivedAtUtc).IsRequired();
        builder.Property(e => e.AttemptCount).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.Error).HasMaxLength(512);

        builder.HasIndex(e => e.MessageKey).IsUnique();
        builder.HasIndex(e => new { e.Status, e.NextAttemptAt });
        builder.HasIndex(e => e.ReceivedAtUtc);
    }
}
```

- [ ] **Step 3: Write the repository implementations**

First open an existing implementation (`api/src/Wrapsfer.Infrastructure/Persistence/Repositories/ShopeeProductLinkRepository.cs`) to confirm the DbContext class name and constructor idiom, then mirror it. Assuming the context is `ApplicationDbContext` (adjust to what you find):

`ShopeeOrderRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class ShopeeOrderRepository(ApplicationDbContext context) : IShopeeOrderRepository
{
    public Task<ShopeeOrder?> GetByIdAsync(Guid id, string tenantId, CancellationToken cancellationToken = default) =>
        context.Set<ShopeeOrder>()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == tenantId, cancellationToken);

    public Task<ShopeeOrder?> GetByOrderSnAsync(string orderSn, string tenantId,
        CancellationToken cancellationToken = default) =>
        context.Set<ShopeeOrder>()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderSn == orderSn && o.TenantId == tenantId, cancellationToken);

    public async Task<(IReadOnlyList<ShopeeOrder> Items, int TotalCount)> ListAsync(
        string tenantId,
        ShopeeOrderStatus? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ShopeeOrder> query = context.Set<ShopeeOrder>()
            .Where(o => o.TenantId == tenantId);

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = $"%{search.Trim()}%";
            query = query.Where(o =>
                EF.Functions.ILike(o.OrderSn, term) ||
                (o.TrackingNumber != null && EF.Functions.ILike(o.TrackingNumber, term)));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        IReadOnlyList<ShopeeOrder> items = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<ShopeeOrder>> ListAwaitingTrackingAsync(
        DateTime arrangedBefore, int batchSize, CancellationToken cancellationToken = default) =>
        await context.Set<ShopeeOrder>()
            .Include(o => o.Items)
            .Where(o => o.Status == ShopeeOrderStatus.AwaitingTracking
                        && o.ShipmentArrangedAt != null
                        && o.ShipmentArrangedAt < arrangedBefore)
            .OrderBy(o => o.ShipmentArrangedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public void Add(ShopeeOrder order) => context.Set<ShopeeOrder>().Add(order);
}
```

`ShopeeWebhookEventRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class ShopeeWebhookEventRepository(ApplicationDbContext context) : IShopeeWebhookEventRepository
{
    public Task<bool> ExistsByMessageKeyAsync(string messageKey, CancellationToken cancellationToken = default) =>
        context.Set<ShopeeWebhookEvent>().AnyAsync(e => e.MessageKey == messageKey, cancellationToken);

    public async Task<IReadOnlyList<ShopeeWebhookEvent>> ListPendingAsync(
        DateTime now, int batchSize, CancellationToken cancellationToken = default) =>
        await context.Set<ShopeeWebhookEvent>()
            .Where(e => e.Status == ShopeeWebhookEventStatus.Pending
                        || (e.Status == ShopeeWebhookEventStatus.Failed
                            && e.NextAttemptAt != null
                            && e.NextAttemptAt <= now))
            .OrderBy(e => e.ReceivedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public void Add(ShopeeWebhookEvent webhookEvent) => context.Set<ShopeeWebhookEvent>().Add(webhookEvent);
}
```

Add to `ShopeeShopConnectionRepository.cs` (mirror the existing method style):

```csharp
    public Task<ShopeeShopConnection?> GetByShopIdAsync(long shopId, CancellationToken cancellationToken = default) =>
        context.Set<ShopeeShopConnection>()
            .FirstOrDefaultAsync(c => c.ShopId == shopId, cancellationToken);

    public async Task<IReadOnlyList<ShopeeShopConnection>> ListAllAsync(
        CancellationToken cancellationToken = default) =>
        await context.Set<ShopeeShopConnection>().ToListAsync(cancellationToken);
```

Register in `Infrastructure/DependencyInjection.cs` next to line ~83:

```csharp
        services.AddScoped<IShopeeOrderRepository, ShopeeOrderRepository>();
        services.AddScoped<IShopeeWebhookEventRepository, ShopeeWebhookEventRepository>();
```

- [ ] **Step 4: Build, generate and inspect the migration**

```bash
cd api
make build
dotnet ef migrations add AddShopeeOrders --project src/Wrapsfer.Infrastructure --startup-project src/Wrapsfer.Api
```

Expected: build succeeds; migration creates `ShopeeOrders`, `ShopeeOrderItems`, `ShopeeWebhookEvents` tables with the unique indexes `(TenantId, OrderSn)` and `MessageKey`. Open the generated migration and verify no unrelated model changes leaked in.

- [ ] **Step 5: Run the full test suite and commit**

```bash
make test
git add src/ && git commit -m "feat(shopee): persist ShopeeOrder and ShopeeWebhookEvent aggregates"
```

---

### Task 4: Shopee gateway — order/logistics endpoints + push signature

**Files:**
- Modify: `api/src/Wrapsfer.Application/Abstractions/Shopee/IShopeeGateway.cs`
- Create (one file per record, in `api/src/Wrapsfer.Application/Abstractions/Shopee/`): `ShopeeOrderList.cs`, `ShopeeOrderDetail.cs`, `ShopeeOrderDetailItem.cs`, `ShopeeShippingParameter.cs`, `ShopeePickupAddress.cs`, `ShopeePickupTimeSlot.cs`, `ShopeeDropoffBranch.cs`, `ShopeeShipOrderRequest.cs`, `ShopeeShipOrderPickup.cs`, `ShopeeShipOrderDropoff.cs`
- Create: `api/src/Wrapsfer.Application/Abstractions/Shopee/IShopeeWebhookSignatureVerifier.cs`
- Modify: `api/src/Wrapsfer.Infrastructure/Shopee/ShopeeRequestSigner.cs` (add `SignPushCallback`)
- Create: `api/src/Wrapsfer.Infrastructure/Shopee/ShopeeWebhookSignatureVerifier.cs`
- Modify: `api/src/Wrapsfer.Infrastructure/Shopee/ShopeeOptions.cs` (add `PushCallbackUrl` + `OrderSync`)
- Create: `api/src/Wrapsfer.Infrastructure/Shopee/ShopeeOrderSyncOptions.cs`
- Modify: `api/src/Wrapsfer.Infrastructure/Shopee/ShopeeHttpGateway.cs` (implement six new methods)
- Create: request/response DTOs in `api/src/Wrapsfer.Infrastructure/Shopee/Dtos/` (mirror the existing DTO style there — `System.Text.Json` attributes, one type per file)
- Test: `api/tests/Wrapsfer.Application.Tests/Shopee/ShopeeWebhookSignatureVerifierTests.cs` (or Infrastructure test location if signer tests already live elsewhere — check `tests/` for existing `ShopeeRequestSigner` tests and colocate)

**Interfaces:**
- Produces (exact signatures later tasks consume):

```csharp
// records
public sealed record ShopeeOrderList(IReadOnlyList<string> OrderSns, bool HasMore, string? NextCursor);
public sealed record ShopeeOrderDetailItem(long ItemId, long ModelId, string? ItemName, string? ModelName, string? ItemSku, int Quantity);
public sealed record ShopeeOrderDetail(string OrderSn, string Status, string? Region, string? BuyerUsername,
    string? RecipientName, string? RecipientPhone, string? RecipientAddress, decimal TotalAmount,
    string? Currency, decimal? CodAmount, string? ShippingCarrier, DateTime? ShipByDate,
    IReadOnlyList<ShopeeOrderDetailItem> Items);
public sealed record ShopeePickupTimeSlot(string PickupTimeId, DateTime Date, string? TimeText);
public sealed record ShopeePickupAddress(long AddressId, string Address, IReadOnlyList<ShopeePickupTimeSlot> TimeSlots);
public sealed record ShopeeDropoffBranch(long BranchId, string Address);
public sealed record ShopeeShippingParameter(bool SupportsPickup, bool SupportsDropoff,
    IReadOnlyList<ShopeePickupAddress> PickupAddresses, IReadOnlyList<ShopeeDropoffBranch> DropoffBranches);
public sealed record ShopeeShipOrderPickup(long AddressId, string PickupTimeId);
public sealed record ShopeeShipOrderDropoff(long? BranchId);
public sealed record ShopeeShipOrderRequest(string OrderSn, ShopeeShipOrderPickup? Pickup, ShopeeShipOrderDropoff? Dropoff);

// IShopeeGateway additions
Task<Result<ShopeeOrderList>> GetOrderListAsync(long shopId, string accessToken,
    DateTime updatedFrom, DateTime updatedTo, string? cursor, int pageSize, CancellationToken cancellationToken = default);
Task<Result<ShopeeOrderDetail>> GetOrderDetailAsync(long shopId, string accessToken,
    string orderSn, CancellationToken cancellationToken = default);
Task<Result<ShopeeShippingParameter>> GetShippingParameterAsync(long shopId, string accessToken,
    string orderSn, CancellationToken cancellationToken = default);
Task<Result> ShipOrderAsync(long shopId, string accessToken,
    ShopeeShipOrderRequest request, CancellationToken cancellationToken = default);
Task<Result<string?>> GetTrackingNumberAsync(long shopId, string accessToken,
    string orderSn, CancellationToken cancellationToken = default);
// Calls create_shipping_document then download_shipping_document; returns the PDF bytes.
Task<Result<byte[]>> DownloadShippingDocumentAsync(long shopId, string accessToken,
    string orderSn, CancellationToken cancellationToken = default);

// verifier
public interface IShopeeWebhookSignatureVerifier
{
    bool Verify(string? authorizationHeader, string requestBody);
}
```

- Shopee v2 endpoints used: `/api/v2/order/get_order_list` (`time_range_field=update_time`, `time_from`, `time_to`, `cursor`, `page_size`), `/api/v2/order/get_order_detail` (`order_sn_list`, `response_optional_fields=buyer_username,recipient_address,total_amount,item_list,shipping_carrier,ship_by_date,cod,currency`), `/api/v2/logistics/get_shipping_parameter`, `/api/v2/logistics/ship_order`, `/api/v2/logistics/get_tracking_number`, `/api/v2/logistics/create_shipping_document`, `/api/v2/logistics/download_shipping_document`.
- Push signature: lowercase-hex `HMAC-SHA256(partner_key, "{push_callback_url}|{raw_body}")`, compared against the `Authorization` header with `CryptographicOperations.FixedTimeEquals`.

- [ ] **Step 1: Write the failing verifier test**

```csharp
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Wrapsfer.Infrastructure.Shopee;

namespace Wrapsfer.Application.Tests.Shopee;

public sealed class ShopeeWebhookSignatureVerifierTests
{
    private const string Url = "https://api.wrapsfer.com/api/v1/shopee/webhook";
    private const string Key = "test-partner-key";

    private static ShopeeWebhookSignatureVerifier CreateVerifier() =>
        new(Options.Create(new ShopeeOptions
        {
            PartnerId = 1001,
            PartnerKey = Key,
            PushCallbackUrl = Url
        }));

    private static string Sign(string body)
    {
        byte[] hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Key), Encoding.UTF8.GetBytes($"{Url}|{body}"));
        return Convert.ToHexStringLower(hash);
    }

    [Fact]
    public void Verify_WithValidSignature_ReturnsTrue()
    {
        string body = "{\"code\":3,\"shop_id\":123}";
        CreateVerifier().Verify(Sign(body), body).Should().BeTrue();
    }

    [Fact]
    public void Verify_WithWrongSignature_ReturnsFalse()
    {
        CreateVerifier().Verify("deadbeef", "{\"code\":3}").Should().BeFalse();
    }

    [Fact]
    public void Verify_WithMissingHeader_ReturnsFalse()
    {
        CreateVerifier().Verify(null, "{\"code\":3}").Should().BeFalse();
    }
}
```

Note: this test constructs the Infrastructure verifier directly — check how existing Application.Tests reference Infrastructure types (there is precedent: tests reference `Wrapsfer.Infrastructure.Shopee.ShopeeOptions`). If Infrastructure internals are not visible, make `ShopeeWebhookSignatureVerifier` `public` (the class holds no secrets).

- [ ] **Step 2: Run to verify it fails** — `dotnet test --filter "FullyQualifiedName~ShopeeWebhookSignatureVerifierTests"` → compilation FAILURE.

- [ ] **Step 3: Implement options, signer addition, verifier**

Append to `ShopeeRequestSigner.cs`:

```csharp
    /// <summary>Push callbacks: base string is {push_url}|{raw_body}, keyed by the partner key.</summary>
    public static string SignPushCallback(string partnerKey, string pushUrl, string body) =>
        Sign(partnerKey, $"{pushUrl}|{body}");
```

`ShopeeOrderSyncOptions.cs`:

```csharp
namespace Wrapsfer.Infrastructure.Shopee;

public sealed class ShopeeOrderSyncOptions
{
    public bool Enabled { get; set; }
    public int ReconciliationIntervalMinutes { get; set; } = 60;
    public int WindowHours { get; set; } = 24;
    public int TrackingRetryThresholdMinutes { get; set; } = 30;
    public int WebhookDispatchIntervalSeconds { get; set; } = 10;
    public int WebhookMaxAttempts { get; set; } = 5;
    public int WebhookBatchSize { get; set; } = 50;
}
```

Add to `ShopeeOptions.cs` (after `StockSync`):

```csharp
    /// <summary>
    /// The exact public push URL registered in the Shopee Open Platform console
    /// (e.g. https://api.wrapsfer.com/api/v1/shopee/webhook). Shopee signs each push
    /// over this URL plus the raw body, so it must match the console value byte-for-byte.
    /// </summary>
    public string PushCallbackUrl { get; set; } = string.Empty;

    public ShopeeOrderSyncOptions OrderSync { get; set; } = new();
```

`ShopeeWebhookSignatureVerifier.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions.Shopee;

namespace Wrapsfer.Infrastructure.Shopee;

public sealed class ShopeeWebhookSignatureVerifier(
    IOptions<ShopeeOptions> options) : IShopeeWebhookSignatureVerifier
{
    public bool Verify(string? authorizationHeader, string requestBody)
    {
        ShopeeOptions shopeeOptions = options.Value;
        if (string.IsNullOrWhiteSpace(shopeeOptions.PartnerKey)
            || string.IsNullOrWhiteSpace(shopeeOptions.PushCallbackUrl)
            || string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return false;
        }

        string expected = ShopeeRequestSigner.SignPushCallback(
            shopeeOptions.PartnerKey, shopeeOptions.PushCallbackUrl, requestBody);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(authorizationHeader.Trim()));
    }
}
```

Register in `ShopeeServiceExtensions.cs` (next to the gateway registration): `services.AddScoped<IShopeeWebhookSignatureVerifier, ShopeeWebhookSignatureVerifier>();`

- [ ] **Step 4: Run verifier tests** — expected PASS.

- [ ] **Step 5: Implement the gateway methods**

Add the record files listed above under `Application/Abstractions/Shopee/` (one per file, exactly as in the Interfaces block). Add the six method signatures to `IShopeeGateway`. Then implement in `ShopeeHttpGateway.cs`, following the file's existing structure exactly (options `IsConfigured` guard → build signed URL via the existing `BuildShopRequestUrl` helper → `try/catch` with the same `OperationCanceledException` filter → check both HTTP status and body `error` field → log via `LogFailureAsync`). New path constants:

```csharp
    private const string GetOrderListPath = "/api/v2/order/get_order_list";
    private const string GetOrderDetailPath = "/api/v2/order/get_order_detail";
    private const string GetShippingParameterPath = "/api/v2/logistics/get_shipping_parameter";
    private const string ShipOrderPath = "/api/v2/logistics/ship_order";
    private const string GetTrackingNumberPath = "/api/v2/logistics/get_tracking_number";
    private const string CreateShippingDocumentPath = "/api/v2/logistics/create_shipping_document";
    private const string DownloadShippingDocumentPath = "/api/v2/logistics/download_shipping_document";
```

Implementation notes (bind DTOs in `Infrastructure/Shopee/Dtos/` with `[JsonPropertyName]`, mirroring `ShopeeShopInfoResponse`):
- `GetOrderListAsync`: GET with `time_range_field=update_time&time_from={unix}&time_to={unix}&page_size={n}` plus `&cursor={cursor}` when non-empty. Map `response.order_list[].order_sn`, `response.more`, `response.next_cursor`. Failure error: `ShopeeOrderErrors.OrderListFetchFailed`.
- `GetOrderDetailAsync`: GET with `order_sn_list={orderSn}&response_optional_fields=buyer_username,recipient_address,total_amount,item_list,shipping_carrier,ship_by_date,cod,currency`. Map first element of `response.order_list`: `order_sn`, `order_status`, `region`, `buyer_username`, `recipient_address.{name,phone,full_address}`, `total_amount`, `currency`, `cod` (bool — when true set `CodAmount = total_amount`, else null), `shipping_carrier`, `ship_by_date` (unix seconds → `DateTimeOffset.FromUnixTimeSeconds(x).UtcDateTime`, null when 0), items from `item_list[]`: `item_id`, `model_id`, `item_name`, `model_name`, `item_sku` (fall back to `model_sku` when `item_sku` empty), `model_quantity_purchased`. Failure error: `ShopeeOrderErrors.OrderDetailFetchFailed`.
- `GetShippingParameterAsync`: GET with `order_sn={orderSn}`. Map `response.info_needed.pickup`/`dropoff` presence to `SupportsPickup`/`SupportsDropoff`; `response.pickup.address_list[]` → `address_id`, `address`, `time_slot_list[]` (`pickup_time_id`, `date` unix → UTC, `time_text`); `response.dropoff.branch_list[]` → `branch_id`, `address`. Failure: `ShopeeOrderErrors.ShippingParameterFetchFailed`.
- `ShipOrderAsync`: POST JSON body `{ order_sn, pickup: { address_id, pickup_time_id } | dropoff: { branch_id } }` — serialize only the branch matching the request. Failure: `ShopeeOrderErrors.ShipmentRequestFailed`.
- `GetTrackingNumberAsync`: GET with `order_sn={orderSn}`. Success with empty/missing `response.tracking_number` returns `Result<string?>.Success(null)` (not yet assigned — NOT an error). Failure: `ShopeeOrderErrors.TrackingNumberFetchFailed`.
- `DownloadShippingDocumentAsync`: POST `create_shipping_document` body `{ order_list: [{ order_sn }] }`; treat Shopee error `logistics.shipping_document_exist` (document already created) as success. Then POST `download_shipping_document` body `{ order_list: [{ order_sn }] }` and read the response as bytes (`response.Content.ReadAsByteArrayAsync`) — it is the PDF, not JSON (a JSON body indicates an error: log it and fail). Failure: `ShopeeOrderErrors.LabelFetchFailed`.

- [ ] **Step 6: Build and run the full Shopee test filter**

```bash
make build
dotnet test --filter "FullyQualifiedName~Shopee"
```

Expected: build clean (warnings are errors), existing + new tests PASS.

- [ ] **Step 7: Commit**

```bash
git add src/ tests/
git commit -m "feat(shopee): add order/logistics gateway endpoints and push signature verification"
```

---

### Task 5: Application services — token refresher, order ingestion, cancellation handling

**Files:**
- Create: `api/src/Wrapsfer.Application/Shopee/Services/ShopeeConnectionTokenRefresher.cs`
- Create: `api/src/Wrapsfer.Application/Shopee/Services/ShopeeOrderCancellationService.cs`
- Create: `api/src/Wrapsfer.Application/Shopee/Services/ShopeeOrderIngestionService.cs`
- Modify: `api/src/Wrapsfer.Application/DependencyInjection.cs` (register all three, next to `ShopeeStockSyncProcessor` at ~line 41)
- Test: `api/tests/Wrapsfer.Application.Tests/Shopee/ShopeeOrderIngestionServiceTests.cs`
- Test: `api/tests/Wrapsfer.Application.Tests/Shopee/ShopeeOrderCancellationServiceTests.cs`

**Interfaces:**
- Consumes: `IShopeeGateway.GetOrderDetailAsync`/`RefreshAccessTokenAsync`, `IShopeeOrderRepository`, `IShopeeProductLinkRepository.ListByTenantAsync`, `IWaybillRepository.GetByIdWithItemsAsync`, `StockReservationService.ReleaseItemsAsync`, `ShopeeOrder` domain API.
- Produces:
  - `ShopeeConnectionTokenRefresher.RefreshIfNeededAsync(ShopeeShopConnection connection, CancellationToken ct) : Task` — refreshes when the access token expires within 5 minutes, saving via `IUnitOfWork`; mirrors the private logic in `ShopeeStockSyncProcessor` (do not modify that class).
  - `ShopeeOrderIngestionService.IngestOrderAsync(ShopeeShopConnection connection, string orderSn, CancellationToken ct) : Task<Result>` — fetch detail → resolve items → create or update the order → run cancellation handling when Shopee says CANCELLED → save. Behavior matrix:
    - Order unknown + Shopee status `UNPAID` or `CANCELLED` → success, nothing stored.
    - Order unknown + any other status → create (`ShopeeOrder.Create`), add, save.
    - Order known → `ApplyShopeeSnapshot`; if Shopee status is `CANCELLED` and internal status isn't `Cancelled` → `ShopeeOrderCancellationService.HandleCancellationAsync`; save.
  - `ShopeeOrderCancellationService.HandleCancellationAsync(ShopeeOrder order, CancellationToken ct) : Task<Result>` — `order.MarkCancelled(now)`; when a linked waybill exists and is Draft/Packed: `waybill.Cancel("Shopee order cancelled")` + `ReleaseItemsAsync`; HandedOff/Cancelled waybills untouched. Does NOT save (caller saves).
  - `public const string CancellationReason = "Shopee order cancelled"` on the cancellation service.

- [ ] **Step 1: Write the failing tests**

`ShopeeOrderCancellationServiceTests.cs` (Moq; construct a `Waybill` in each status via its domain API):

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee;

public sealed class ShopeeOrderCancellationServiceTests
{
    private static readonly DateTime Now = new(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);
    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IStockMovementRepository> _stockMovementRepository = new();

    // NOTE: StockReservationService is concrete — check its constructor and either
    // mock its repository dependencies (as here) or extract the existing test helper
    // used by CancelWaybillCommandHandler tests. Mirror whatever
    // tests/Wrapsfer.Application.Tests already does for StockReservationService.

    private static ShopeeOrder CreateOrder(Guid? waybillId = null)
    {
        ShopeeOrderSnapshot snapshot = new(
            "READY_TO_SHIP", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", null);
        ShopeeOrder order = ShopeeOrder.Create(
            "tenant-a", "SN1", "MY", snapshot,
            [new ShopeeOrderItemSnapshot(1, 0, "n", null, null, 1, Guid.NewGuid())], Now).Value;
        if (waybillId.HasValue)
        {
            order.MarkShipmentArranged("u", Now);
            order.AssignTracking("TRACK1");
            order.LinkWaybill(waybillId.Value);
        }
        return order;
    }

    [Fact]
    public async Task HandleCancellation_WithoutWaybill_JustCancelsOrder()
    {
        ShopeeOrderCancellationService service = CreateService();
        ShopeeOrder order = CreateOrder();
        Result result = await service.HandleCancellationAsync(order, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.Cancelled);
    }

    [Fact]
    public async Task HandleCancellation_WithDraftWaybill_CancelsWaybillAndReleasesStock()
    {
        Waybill waybill = Waybill.Create("tenant-a", new DateOnly(2026, 7, 16), "TRACK1").Value;
        Guid productId = Guid.NewGuid();
        waybill.AddOrIncrementItem(productId, "BARCODE1", 2);
        _waybillRepository
            .Setup(r => r.GetByIdWithItemsAsync(waybill.Id, "tenant-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybill);
        SetUpStockReleaseSuccess();

        ShopeeOrderCancellationService service = CreateService();
        ShopeeOrder order = CreateOrder(waybill.Id);
        Result result = await service.HandleCancellationAsync(order, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        waybill.Status.Should().Be(WaybillStatus.Cancelled);
        waybill.CancellationReason.Should().Be(ShopeeOrderCancellationService.CancellationReason);
    }

    [Fact]
    public async Task HandleCancellation_WithHandedOffWaybill_LeavesWaybillUntouched()
    {
        Waybill waybill = Waybill.Create("tenant-a", new DateOnly(2026, 7, 16), "TRACK1").Value;
        waybill.AddOrIncrementItem(Guid.NewGuid(), "BARCODE1", 2);
        waybill.MarkPacked();
        waybill.MarkHandedOff(waybill.CreatedBy ?? "creator", actingUserIsAdmin: true);
        _waybillRepository
            .Setup(r => r.GetByIdWithItemsAsync(waybill.Id, "tenant-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybill);

        ShopeeOrderCancellationService service = CreateService();
        ShopeeOrder order = CreateOrder(waybill.Id);
        Result result = await service.HandleCancellationAsync(order, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.Cancelled);
        waybill.Status.Should().Be(WaybillStatus.HandedOff);
    }

    // CreateService() and SetUpStockReleaseSuccess() are built from whatever pattern the
    // existing CancelWaybillCommandHandler tests use to construct StockReservationService —
    // find them with: grep -rn "StockReservationService" api/tests/Wrapsfer.Application.Tests/
}
```

`ShopeeOrderIngestionServiceTests.cs` — cover: (a) unknown order with `READY_TO_SHIP` detail is created with items resolved through mocked `IShopeeProductLinkRepository.ListByTenantAsync` (link present → `ReadyToShip`; absent → `NeedsLinking`); (b) unknown order with `UNPAID` detail stores nothing (`orderRepository.Add` never called); (c) known order re-syncs snapshot and a `CANCELLED` detail invokes cancellation. Mock `IShopeeGateway.GetOrderDetailAsync` to return a `ShopeeOrderDetail` fixture with one item (`ItemId=111, ModelId=0, Quantity=2`). Assert with the same style as the cancellation tests above.

- [ ] **Step 2: Run to verify failure** — `dotnet test --filter "FullyQualifiedName~ShopeeOrderIngestion|FullyQualifiedName~ShopeeOrderCancellation"` → compilation FAILURE.

- [ ] **Step 3: Implement the three services**

`ShopeeConnectionTokenRefresher.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Application.Shopee.Services;

/// <summary>
/// Refreshes a connection's Shopee tokens when they are about to expire. Shared by the
/// order sync/webhook/reconciliation paths (the stock sync processor keeps its own copy).
/// </summary>
public sealed class ShopeeConnectionTokenRefresher(
    IShopeeGateway shopeeGateway,
    IUnitOfWork unitOfWork,
    ILogger<ShopeeConnectionTokenRefresher> logger)
{
    private static readonly TimeSpan s_refreshWindow = TimeSpan.FromMinutes(5);

    public async Task RefreshIfNeededAsync(ShopeeShopConnection connection, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        if (connection.AccessTokenExpiresAt > now.Add(s_refreshWindow))
        {
            return;
        }

        Result<ShopeeTokenGrant> grantResult = await shopeeGateway.RefreshAccessTokenAsync(
            connection.RefreshToken, connection.ShopId, cancellationToken);
        if (grantResult.IsFailure)
        {
            logger.LogWarning(
                "Shopee token refresh before order sync failed for tenant {TenantId}, shop {ShopId}: {ErrorCode}",
                connection.TenantId, connection.ShopId, grantResult.Error.Code);
            return;
        }

        ShopeeTokenGrant grant = grantResult.Value;
        Result updateResult = connection.UpdateTokens(
            grant.AccessToken,
            grant.RefreshToken,
            now.AddSeconds(grant.ExpiresInSeconds),
            now.Add(ShopeeShopConnection.RefreshTokenLifetime));
        if (updateResult.IsFailure)
        {
            logger.LogWarning(
                "Shopee token refresh before order sync returned invalid tokens for tenant {TenantId}, shop {ShopId}: {ErrorCode}",
                connection.TenantId, connection.ShopId, updateResult.Error.Code);
            return;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

`ShopeeOrderCancellationService.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Services;

/// <summary>
/// Applies a Shopee-side cancellation to the local order and, when the linked waybill is
/// still cancellable (Draft/Packed), cancels it and releases the reserved stock — the same
/// effect as a manual waybill cancellation. HandedOff waybills are never touched; the
/// cancelled order itself is the attention flag. Callers own SaveChanges.
/// </summary>
public sealed class ShopeeOrderCancellationService(
    IWaybillRepository waybillRepository,
    StockReservationService stockReservationService,
    ILogger<ShopeeOrderCancellationService> logger)
{
    public const string CancellationReason = "Shopee order cancelled";

    public async Task<Result> HandleCancellationAsync(ShopeeOrder order, CancellationToken cancellationToken)
    {
        Result markResult = order.MarkCancelled(DateTime.UtcNow);
        if (markResult.IsFailure)
        {
            return markResult;
        }

        if (order.WaybillId is null)
        {
            return Result.Success();
        }

        Waybill? waybill = await waybillRepository.GetByIdWithItemsAsync(
            order.WaybillId.Value, order.TenantId, cancellationToken);
        if (waybill is null
            || waybill.Status is WaybillStatus.HandedOff or WaybillStatus.Cancelled)
        {
            logger.LogInformation(
                "Shopee order {OrderSn} cancelled but waybill {WaybillId} is not cancellable",
                order.OrderSn, order.WaybillId);
            return Result.Success();
        }

        WaybillStatus previousStatus = waybill.Status;
        Result cancelResult = waybill.Cancel(CancellationReason);
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        if (previousStatus is WaybillStatus.Draft or WaybillStatus.Packed)
        {
            Result releaseResult = await stockReservationService.ReleaseItemsAsync(
                waybill.Items, order.TenantId, cancellationToken);
            if (releaseResult.IsFailure)
            {
                return releaseResult;
            }
        }

        return Result.Success();
    }
}
```

`ShopeeOrderIngestionService.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Services;

/// <summary>
/// Fetches a Shopee order's detail and upserts the local ShopeeOrder, resolving each
/// line against the tenant's product links. Shared by the webhook consumer, the
/// reconciliation job, and the on-demand relink command.
/// </summary>
public sealed class ShopeeOrderIngestionService(
    IShopeeGateway shopeeGateway,
    IShopeeOrderRepository orderRepository,
    IShopeeProductLinkRepository linkRepository,
    ShopeeOrderCancellationService cancellationService,
    IUnitOfWork unitOfWork,
    ILogger<ShopeeOrderIngestionService> logger)
{
    private const string StatusUnpaid = "UNPAID";
    private const string StatusCancelled = "CANCELLED";

    public async Task<Result> IngestOrderAsync(
        ShopeeShopConnection connection, string orderSn, CancellationToken cancellationToken)
    {
        Result<ShopeeOrderDetail> detailResult = await shopeeGateway.GetOrderDetailAsync(
            connection.ShopId, connection.AccessToken, orderSn, cancellationToken);
        if (detailResult.IsFailure)
        {
            return Result.Failure(detailResult.Error);
        }

        ShopeeOrderDetail detail = detailResult.Value;
        ShopeeOrder? order = await orderRepository.GetByOrderSnAsync(
            orderSn, connection.TenantId, cancellationToken);

        IReadOnlyList<ShopeeOrderItemSnapshot> items =
            await ResolveItemsAsync(connection.TenantId, detail.Items, cancellationToken);
        ShopeeOrderSnapshot snapshot = ToSnapshot(detail);

        if (order is null)
        {
            if (detail.Status is StatusUnpaid or StatusCancelled)
            {
                return Result.Success();
            }

            Result<ShopeeOrder> createResult = ShopeeOrder.Create(
                connection.TenantId, detail.OrderSn, detail.Region ?? connection.Region,
                snapshot, items, DateTime.UtcNow);
            if (createResult.IsFailure)
            {
                logger.LogWarning(
                    "Shopee order {OrderSn} for tenant {TenantId} could not be created: {ErrorCode}",
                    orderSn, connection.TenantId, createResult.Error.Code);
                return Result.Failure(createResult.Error);
            }

            orderRepository.Add(createResult.Value);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        Result applyResult = order.ApplyShopeeSnapshot(snapshot, items, DateTime.UtcNow);
        if (applyResult.IsFailure)
        {
            return applyResult;
        }

        if (detail.Status == StatusCancelled && order.Status != ShopeeOrderStatus.Cancelled)
        {
            Result cancellationResult =
                await cancellationService.HandleCancellationAsync(order, cancellationToken);
            if (cancellationResult.IsFailure)
            {
                return cancellationResult;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<IReadOnlyList<ShopeeOrderItemSnapshot>> ResolveItemsAsync(
        string tenantId,
        IReadOnlyList<ShopeeOrderDetailItem> items,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ShopeeProductLink> links =
            await linkRepository.ListByTenantAsync(tenantId, cancellationToken);
        Dictionary<(long ItemId, long ModelId), Guid> productByUnit = links.ToDictionary(
            l => (l.ShopeeItemId, l.ShopeeModelId), l => l.ProductId);

        return items
            .Select(i => new ShopeeOrderItemSnapshot(
                i.ItemId,
                i.ModelId,
                i.ItemName,
                i.ModelName,
                i.ItemSku,
                i.Quantity,
                productByUnit.TryGetValue((i.ItemId, i.ModelId), out Guid productId)
                    ? productId
                    : null))
            .ToList();
    }

    private static ShopeeOrderSnapshot ToSnapshot(ShopeeOrderDetail detail) => new(
        detail.Status,
        detail.BuyerUsername,
        detail.RecipientName,
        detail.RecipientPhone,
        detail.RecipientAddress,
        detail.TotalAmount,
        detail.Currency,
        detail.CodAmount,
        detail.ShippingCarrier,
        detail.ShipByDate);
}
```

Register in `Application/DependencyInjection.cs`:

```csharp
        services.AddScoped<ShopeeConnectionTokenRefresher>();
        services.AddScoped<ShopeeOrderCancellationService>();
        services.AddScoped<ShopeeOrderIngestionService>();
```

Note: verify `IShopeeProductLinkRepository.ListByTenantAsync(tenantId, ct)` exists (it is used by `ShopeeStockSyncProcessor.RunForTenantAsync`); if the exact name differs, use the actual method.

- [ ] **Step 4: Run tests** — `dotnet test --filter "FullyQualifiedName~ShopeeOrderIngestion|FullyQualifiedName~ShopeeOrderCancellation"` → PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "feat(shopee): add order ingestion and cancellation services"
```

---

### Task 6: Shipment completion service — tracking number → Waybill

**Files:**
- Create: `api/src/Wrapsfer.Application/Shopee/Services/ShopeeOrderShipmentCompletionService.cs`
- Modify: `api/src/Wrapsfer.Application/DependencyInjection.cs` (register)
- Test: `api/tests/Wrapsfer.Application.Tests/Shopee/ShopeeOrderShipmentCompletionServiceTests.cs`

**Interfaces:**
- Consumes: `IWaybillRepository.ExistsByNumberAsync/Add`, `IProductRepository.GetByIdsAsync`, `StockReservationService.ReserveItemsAsync`, `Waybill.Create/AddOrIncrementItem`, `ShopeeOrder.AssignTracking/LinkWaybill/MarkShipmentFailed`.
- Produces: `ShopeeOrderShipmentCompletionService.CompleteAsync(ShopeeOrder order, string trackingNumber, CancellationToken ct) : Task<Result>` — the single place a Shopee order becomes a Waybill. Saves on both success and handled-failure paths.

- [ ] **Step 1: Write the failing tests** — cover:
  1. **Happy path:** order in `AwaitingTracking` with one resolved item; `ExistsByNumberAsync` false; `GetByIdsAsync` returns the product (use the existing product test builder in `tests/Wrapsfer.Application.Tests` — find with `grep -rn "Product.Create" api/tests/Wrapsfer.Application.Tests | head`); stock reservation succeeds → `Result.IsSuccess`, `waybillRepository.Add` called once, order `Shipped` with `TrackingNumber` and `WaybillId` set.
  2. **Tracking collision:** `ExistsByNumberAsync` true → failure `ShopeeOrderErrors.TrackingNumberConflict`, order `ShipmentFailed`, `LastShipError` populated, `Add` never called.
  3. **Reservation failure:** reservation returns failure → order `ShipmentFailed`, `Add`-ed waybill not saved as success (service returns the reservation error).
  4. **Wrong state:** order in `ReadyToShip` (never arranged) → failure `ShopeeOrderErrors.NotAwaitingTracking`, nothing mutated.

Test skeleton mirrors Task 5's mock style; write all four `[Fact]`s with concrete asserts.

- [ ] **Step 2: Run to verify failure** — `dotnet test --filter "FullyQualifiedName~ShopeeOrderShipmentCompletion"` → compilation FAILURE.

- [ ] **Step 3: Implement**

```csharp
using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Services;

/// <summary>
/// Completes an arranged Shopee shipment once the tracking number is known: assigns the
/// tracking number, creates the Wrapsfer waybill (waybill number = tracking number,
/// packaging date = current UTC date), reserves stock, and links the two. Idempotent per
/// order — a Shipped order is left untouched.
/// </summary>
public sealed class ShopeeOrderShipmentCompletionService(
    IWaybillRepository waybillRepository,
    IProductRepository productRepository,
    StockReservationService stockReservationService,
    IUnitOfWork unitOfWork,
    ILogger<ShopeeOrderShipmentCompletionService> logger)
{
    public async Task<Result> CompleteAsync(
        ShopeeOrder order, string trackingNumber, CancellationToken cancellationToken)
    {
        if (order.Status == ShopeeOrderStatus.Shipped)
        {
            return Result.Success();
        }

        if (order.Status != ShopeeOrderStatus.AwaitingTracking)
        {
            return Result.Failure(ShopeeOrderErrors.NotAwaitingTracking);
        }

        if (order.HasUnresolvedItems)
        {
            return await FailAsync(order, ShopeeOrderErrors.ItemsNotLinked, cancellationToken);
        }

        bool numberTaken = await waybillRepository.ExistsByNumberAsync(
            trackingNumber, order.TenantId, cancellationToken);
        if (numberTaken)
        {
            return await FailAsync(order, ShopeeOrderErrors.TrackingNumberConflict, cancellationToken);
        }

        Result assignResult = order.AssignTracking(trackingNumber);
        if (assignResult.IsFailure)
        {
            return assignResult;
        }

        IReadOnlyList<Product> products = await productRepository.GetByIdsAsync(
            order.Items.Select(i => i.ProductId!.Value), order.TenantId, cancellationToken);
        Dictionary<Guid, Product> productsById = products.ToDictionary(p => p.Id);

        Result<Waybill> waybillResult = Waybill.Create(
            order.TenantId, DateOnly.FromDateTime(DateTime.UtcNow), order.TrackingNumber!);
        if (waybillResult.IsFailure)
        {
            return await FailAsync(order, waybillResult.Error, cancellationToken);
        }

        Waybill waybill = waybillResult.Value;
        foreach (ShopeeOrderItem item in order.Items)
        {
            if (!productsById.TryGetValue(item.ProductId!.Value, out Product? product))
            {
                return await FailAsync(order, ShopeeOrderErrors.ProductNotFoundForItem, cancellationToken);
            }

            Result<WaybillItem> addResult = waybill.AddOrIncrementItem(
                product.Id, product.Barcode, item.Quantity);
            if (addResult.IsFailure)
            {
                return await FailAsync(order, addResult.Error, cancellationToken);
            }
        }

        Result reserveResult = await stockReservationService.ReserveItemsAsync(
            waybill.Items, order.TenantId, cancellationToken);
        if (reserveResult.IsFailure)
        {
            return await FailAsync(order, reserveResult.Error, cancellationToken);
        }

        waybillRepository.Add(waybill);
        Result linkResult = order.LinkWaybill(waybill.Id);
        if (linkResult.IsFailure)
        {
            return linkResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Shopee order {OrderSn} completed: waybill {WaybillNumber} created for tenant {TenantId}",
            order.OrderSn, waybill.WaybillNumber, order.TenantId);
        return Result.Success();
    }

    private async Task<Result> FailAsync(ShopeeOrder order, Error error, CancellationToken cancellationToken)
    {
        Result failResult = order.MarkShipmentFailed(error.Description, DateTime.UtcNow);
        if (failResult.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Failure(error);
    }
}
```

Register: `services.AddScoped<ShopeeOrderShipmentCompletionService>();`

Note: `FailAsync` on the tracking-collision path runs before `AssignTracking`, so the order is still `AwaitingTracking` and `MarkShipmentFailed` succeeds. After `AssignTracking` the status is unchanged (`AwaitingTracking`), so later failures also transition cleanly.

- [ ] **Step 4: Run tests** — PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "feat(shopee): create waybill from tracked Shopee shipment"
```

---

### Task 7: Webhook ingestion — command, endpoint, dispatch job

**Files:**
- Create: `api/src/Wrapsfer.Application/Shopee/Commands/IngestShopeeWebhook/IngestShopeeWebhookCommand.cs`
- Create: `api/src/Wrapsfer.Application/Shopee/Commands/IngestShopeeWebhook/IngestShopeeWebhookCommandHandler.cs`
- Create: `api/src/Wrapsfer.Application/Shopee/Services/ShopeeWebhookEventProcessor.cs`
- Create: `api/src/Wrapsfer.Application/Shopee/Services/ShopeeWebhookRunSummary.cs`
- Create: `api/src/Wrapsfer.Infrastructure/BackgroundServices/ShopeeWebhookDispatchJob.cs`
- Modify: `api/src/Wrapsfer.Api/Controllers/V1/ShopeeController.cs` (add webhook action)
- Modify: `api/src/Wrapsfer.Application/DependencyInjection.cs` (register processor), `api/src/Wrapsfer.Infrastructure/DependencyInjection.cs` (register hosted job at ~line 118)
- Test: `api/tests/Wrapsfer.Application.Tests/Shopee/IngestShopeeWebhookCommandHandlerTests.cs`
- Test: `api/tests/Wrapsfer.Application.Tests/Shopee/ShopeeWebhookEventProcessorTests.cs`

**Interfaces:**
- Consumes: `IShopeeWebhookSignatureVerifier`, `IShopeeWebhookEventRepository`, `IShopeeShopConnectionRepository.GetByShopIdAsync`, `ShopeeOrderIngestionService`, `ShopeeOrderShipmentCompletionService`, `ShopeeConnectionTokenRefresher`, `IShopeeGateway.GetTrackingNumberAsync`.
- Produces:
  - `record IngestShopeeWebhookCommand(string Body, string? AuthorizationHeader) : ICommand`
  - `ShopeeWebhookEventProcessor.RunAsync(int batchSize, int maxAttempts, CancellationToken ct) : Task<ShopeeWebhookRunSummary>`
  - `record ShopeeWebhookRunSummary(int ProcessedCount, int FailedCount, int IgnoredCount)`
  - Push codes: `public const int OrderStatusPushCode = 3; public const int TrackingNumberPushCode = 4;` on the processor. **Verify both codes against the Shopee Open Platform console during manual E2E** — they are configurable constants for exactly this reason.
  - HTTP: `POST api/v1/shopee/webhook` → 200 on stored/duplicate, 401 on bad signature, 400 on malformed body.

- [ ] **Step 1: Write the failing handler tests**

`IngestShopeeWebhookCommandHandlerTests.cs` — mock verifier + repository + unit of work; body fixture `{"code":3,"shop_id":123456,"data":{"ordersn":"SN1","status":"READY_TO_SHIP"}}`:
  1. Valid signature + new message → event added with `Status=Pending`, `ShopId=123456`, `Code=3`; saved; success.
  2. Valid signature + `ExistsByMessageKeyAsync` true → success, `Add` never called (idempotent redelivery).
  3. Invalid signature → failure `ShopeeWebhookEventErrors.InvalidSignature`, nothing stored.
  4. Non-JSON body → failure `ShopeeWebhookEventErrors.MalformedPushBody`.
  5. Code `99` (unhandled) → event stored but `MarkIgnored()` applied (`Status=Ignored`); success.

- [ ] **Step 2: Run to verify failure.**

- [ ] **Step 3: Implement command + handler**

`IngestShopeeWebhookCommand.cs`:

```csharp
using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Shopee.Commands.IngestShopeeWebhook;

public sealed record IngestShopeeWebhookCommand(string Body, string? AuthorizationHeader) : ICommand;
```

`IngestShopeeWebhookCommandHandler.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Commands.IngestShopeeWebhook;

internal sealed class IngestShopeeWebhookCommandHandler(
    IShopeeWebhookSignatureVerifier signatureVerifier,
    IShopeeWebhookEventRepository eventRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<IngestShopeeWebhookCommand>
{
    public async Task<Result> Handle(IngestShopeeWebhookCommand request, CancellationToken cancellationToken)
    {
        if (!signatureVerifier.Verify(request.AuthorizationHeader, request.Body))
        {
            return Result.Failure(ShopeeWebhookEventErrors.InvalidSignature);
        }

        int code;
        long shopId;
        try
        {
            using JsonDocument document = JsonDocument.Parse(request.Body);
            if (!document.RootElement.TryGetProperty("code", out JsonElement codeElement)
                || !document.RootElement.TryGetProperty("shop_id", out JsonElement shopIdElement))
            {
                return Result.Failure(ShopeeWebhookEventErrors.MalformedPushBody);
            }

            code = codeElement.GetInt32();
            shopId = shopIdElement.GetInt64();
        }
        catch (JsonException)
        {
            return Result.Failure(ShopeeWebhookEventErrors.MalformedPushBody);
        }

        string messageKey = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.Body)));

        bool duplicate = await eventRepository.ExistsByMessageKeyAsync(messageKey, cancellationToken);
        if (duplicate)
        {
            return Result.Success();
        }

        Result<ShopeeWebhookEvent> createResult = ShopeeWebhookEvent.Create(
            shopId, code, messageKey, request.Body, DateTime.UtcNow);
        if (createResult.IsFailure)
        {
            return Result.Failure(createResult.Error);
        }

        ShopeeWebhookEvent webhookEvent = createResult.Value;
        if (code is not (ShopeeWebhookEventProcessor.OrderStatusPushCode
            or ShopeeWebhookEventProcessor.TrackingNumberPushCode))
        {
            webhookEvent.MarkIgnored();
        }

        eventRepository.Add(webhookEvent);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

- [ ] **Step 4: Implement the processor + summary record**

`ShopeeWebhookRunSummary.cs`:

```csharp
namespace Wrapsfer.Application.Shopee.Services;

public sealed record ShopeeWebhookRunSummary(int ProcessedCount, int FailedCount, int IgnoredCount);
```

`ShopeeWebhookEventProcessor.cs`:

```csharp
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Services;

/// <summary>
/// Drains stored Shopee push events: order-status pushes flow through the ingestion
/// service; tracking-number pushes complete an arranged shipment. Failures retry with
/// backoff up to maxAttempts, after which the reconciliation job is the safety net.
/// </summary>
public sealed class ShopeeWebhookEventProcessor(
    IShopeeWebhookEventRepository eventRepository,
    IShopeeShopConnectionRepository connectionRepository,
    IShopeeOrderRepository orderRepository,
    IShopeeGateway shopeeGateway,
    ShopeeOrderIngestionService ingestionService,
    ShopeeOrderShipmentCompletionService completionService,
    ShopeeConnectionTokenRefresher tokenRefresher,
    IUnitOfWork unitOfWork,
    ILogger<ShopeeWebhookEventProcessor> logger)
{
    public const int OrderStatusPushCode = 3;
    public const int TrackingNumberPushCode = 4;

    public async Task<ShopeeWebhookRunSummary> RunAsync(
        int batchSize, int maxAttempts, CancellationToken cancellationToken)
    {
        IReadOnlyList<ShopeeWebhookEvent> events =
            await eventRepository.ListPendingAsync(DateTime.UtcNow, batchSize, cancellationToken);

        int processed = 0;
        int failed = 0;
        int ignored = 0;

        foreach (ShopeeWebhookEvent webhookEvent in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Result result;
            try
            {
                result = await ProcessAsync(webhookEvent, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Shopee webhook event {EventId} processing threw", webhookEvent.Id);
                result = Result.Failure(Error.Failure);
            }

            if (result.IsSuccess)
            {
                webhookEvent.MarkProcessed(DateTime.UtcNow);
                processed++;
            }
            else if (result.Error.Code == ShopeeOrderErrors.ConnectionNotFound.Code)
            {
                webhookEvent.MarkIgnored();
                ignored++;
            }
            else
            {
                webhookEvent.MarkFailed(result.Error.Code, DateTime.UtcNow, maxAttempts);
                failed++;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ShopeeWebhookRunSummary(processed, failed, ignored);
    }

    private async Task<Result> ProcessAsync(
        ShopeeWebhookEvent webhookEvent, CancellationToken cancellationToken)
    {
        ShopeeShopConnection? connection =
            await connectionRepository.GetByShopIdAsync(webhookEvent.ShopId, cancellationToken);
        if (connection is null)
        {
            return Result.Failure(ShopeeOrderErrors.ConnectionNotFound);
        }

        string? orderSn = ExtractOrderSn(webhookEvent.Payload);
        if (string.IsNullOrWhiteSpace(orderSn))
        {
            return Result.Failure(ShopeeWebhookEventErrors.MalformedPushBody);
        }

        await tokenRefresher.RefreshIfNeededAsync(connection, cancellationToken);

        if (webhookEvent.Code == OrderStatusPushCode)
        {
            return await ingestionService.IngestOrderAsync(connection, orderSn, cancellationToken);
        }

        return await CompleteTrackingAsync(connection, orderSn, webhookEvent.Payload, cancellationToken);
    }

    private async Task<Result> CompleteTrackingAsync(
        ShopeeShopConnection connection, string orderSn, string payload, CancellationToken cancellationToken)
    {
        ShopeeOrder? order = await orderRepository.GetByOrderSnAsync(
            orderSn, connection.TenantId, cancellationToken);
        if (order is null)
        {
            // Tracking push for an order we never ingested (e.g. arranged in Seller Centre).
            return await ingestionService.IngestOrderAsync(connection, orderSn, cancellationToken);
        }

        if (order.Status != ShopeeOrderStatus.AwaitingTracking)
        {
            return Result.Success();
        }

        string? trackingNumber = ExtractTrackingNumber(payload);
        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            Result<string?> trackingResult = await shopeeGateway.GetTrackingNumberAsync(
                connection.ShopId, connection.AccessToken, orderSn, cancellationToken);
            if (trackingResult.IsFailure)
            {
                return Result.Failure(trackingResult.Error);
            }

            trackingNumber = trackingResult.Value;
        }

        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            // Not assigned yet; the reconciliation job re-polls stuck orders.
            return Result.Success();
        }

        return await completionService.CompleteAsync(order, trackingNumber, cancellationToken);
    }

    private static string? ExtractOrderSn(string payload) =>
        ExtractDataString(payload, "ordersn") ?? ExtractDataString(payload, "order_sn");

    private static string? ExtractTrackingNumber(string payload) =>
        ExtractDataString(payload, "tracking_no") ?? ExtractDataString(payload, "tracking_number");

    private static string? ExtractDataString(string payload, string propertyName)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("data", out JsonElement data)
                && data.TryGetProperty(propertyName, out JsonElement value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
```

`ShopeeWebhookEventProcessorTests.cs` — write these `[Fact]`s with mocks: (1) order-status event routes to ingestion and is `MarkProcessed`; (2) unknown shop id → event `Ignored`; (3) ingestion failure → event `Failed` with `AttemptCount=1`; (4) tracking event with payload tracking number calls completion service for an `AwaitingTracking` order.

- [ ] **Step 5: Add the controller action + dispatch job**

Controller action in `ShopeeController.cs` (after the OAuth callback action):

```csharp
    // Shopee push mechanism. Anonymous by necessity; authenticity is the HMAC signature over
    // the registered push URL + raw body. Must ack fast — Shopee disables slow endpoints —
    // so this only verifies, stores, and returns; processing happens in ShopeeWebhookDispatchJob.
    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        using StreamReader reader = new(Request.Body);
        string body = await reader.ReadToEndAsync(cancellationToken);
        string authorizationHeader = Request.Headers.Authorization.ToString();

        Result result = await sender.Send(
            new IngestShopeeWebhookCommand(body, authorizationHeader), cancellationToken);
        if (result.IsSuccess)
        {
            return Ok();
        }

        return result.Error.Code == ShopeeWebhookEventErrors.InvalidSignature.Code
            ? Unauthorized()
            : BadRequest();
    }
```

`ShopeeWebhookDispatchJob.cs` (mirror `ShopeeStockSyncJob` structure):

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Infrastructure.Shopee;

namespace Wrapsfer.Infrastructure.BackgroundServices;

public sealed class ShopeeWebhookDispatchJob(
    IServiceScopeFactory scopeFactory,
    IOptions<ShopeeOptions> options,
    ILogger<ShopeeWebhookDispatchJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ShopeeOrderSyncOptions syncOptions = options.Value.OrderSync;
        if (!syncOptions.Enabled)
        {
            logger.LogInformation("Shopee webhook dispatch job is disabled by configuration");
            return;
        }

        int intervalSeconds = Math.Max(syncOptions.WebhookDispatchIntervalSeconds, 1);
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(intervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ExecuteOnceAsync(syncOptions, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }

    private async Task ExecuteOnceAsync(ShopeeOrderSyncOptions syncOptions, CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ShopeeWebhookEventProcessor processor =
                scope.ServiceProvider.GetRequiredService<ShopeeWebhookEventProcessor>();

            ShopeeWebhookRunSummary summary = await processor.RunAsync(
                syncOptions.WebhookBatchSize, syncOptions.WebhookMaxAttempts, cancellationToken);
            if (summary.ProcessedCount + summary.FailedCount + summary.IgnoredCount > 0)
            {
                logger.LogInformation(
                    "ShopeeWebhookDispatchJob completed: processed {Processed}, failed {Failed}, ignored {Ignored}",
                    summary.ProcessedCount, summary.FailedCount, summary.IgnoredCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // graceful shutdown mid-run
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ShopeeWebhookDispatchJob iteration failed");
        }
    }
}
```

Register: `services.AddScoped<ShopeeWebhookEventProcessor>();` (Application DI) and `services.AddHostedService<ShopeeWebhookDispatchJob>();` (Infrastructure DI, next to `ShopeeStockSyncJob`).

- [ ] **Step 6: Run tests** — `dotnet test --filter "FullyQualifiedName~ShopeeWebhook"` → PASS; then `make build`.

- [ ] **Step 7: Commit**

```bash
git add src/ tests/
git commit -m "feat(shopee): webhook endpoint, event store, and dispatch processing"
```

---

### Task 8: Reconciliation job

**Files:**
- Create: `api/src/Wrapsfer.Application/Shopee/Services/ShopeeOrderReconciliationProcessor.cs`
- Create: `api/src/Wrapsfer.Infrastructure/BackgroundServices/ShopeeOrderReconciliationJob.cs`
- Modify: both DI files (register processor + hosted job)
- Test: `api/tests/Wrapsfer.Application.Tests/Shopee/ShopeeOrderReconciliationProcessorTests.cs`

**Interfaces:**
- Produces: `ShopeeOrderReconciliationProcessor.RunAsync(int windowHours, int trackingRetryThresholdMinutes, CancellationToken ct) : Task`

- [ ] **Step 1: Write failing tests** — with mocks: (1) one connection, `GetOrderListAsync` returns two SNs on one page (`HasMore=false`) → ingestion called for both; (2) cursor paging: first page `HasMore=true, NextCursor="c2"`, second `HasMore=false` → gateway called twice, second call receives cursor `"c2"`; (3) an order stuck in `AwaitingTracking` gets `GetTrackingNumberAsync` → non-null → completion called; null → completion not called; (4) a gateway failure for one connection does not prevent the next connection from processing.

- [ ] **Step 2: Run to verify failure.**

- [ ] **Step 3: Implement processor**

```csharp
using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Services;

/// <summary>
/// Safety net behind the webhook path: sweeps each connected shop's recently-updated
/// orders through the same ingestion path, and re-polls tracking numbers for orders
/// stuck in AwaitingTracking. Heals dropped pushes and exhausted webhook retries.
/// </summary>
public sealed class ShopeeOrderReconciliationProcessor(
    IShopeeShopConnectionRepository connectionRepository,
    IShopeeOrderRepository orderRepository,
    IShopeeGateway shopeeGateway,
    ShopeeOrderIngestionService ingestionService,
    ShopeeOrderShipmentCompletionService completionService,
    ShopeeConnectionTokenRefresher tokenRefresher,
    ILogger<ShopeeOrderReconciliationProcessor> logger)
{
    private const int OrderListPageSize = 50;
    private const int TrackingRetryBatchSize = 100;

    public async Task RunAsync(
        int windowHours, int trackingRetryThresholdMinutes, CancellationToken cancellationToken)
    {
        IReadOnlyList<ShopeeShopConnection> connections =
            await connectionRepository.ListAllAsync(cancellationToken);

        foreach (ShopeeShopConnection connection in connections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await SweepConnectionAsync(connection, windowHours, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Shopee order reconciliation failed for tenant {TenantId}", connection.TenantId);
            }
        }

        await RetryStuckTrackingAsync(trackingRetryThresholdMinutes, cancellationToken);
    }

    private async Task SweepConnectionAsync(
        ShopeeShopConnection connection, int windowHours, CancellationToken cancellationToken)
    {
        await tokenRefresher.RefreshIfNeededAsync(connection, cancellationToken);

        DateTime to = DateTime.UtcNow;
        DateTime from = to.AddHours(-windowHours);
        string? cursor = null;

        do
        {
            Result<ShopeeOrderList> pageResult = await shopeeGateway.GetOrderListAsync(
                connection.ShopId, connection.AccessToken, from, to, cursor, OrderListPageSize,
                cancellationToken);
            if (pageResult.IsFailure)
            {
                logger.LogWarning(
                    "Shopee order list fetch failed for tenant {TenantId}: {ErrorCode}",
                    connection.TenantId, pageResult.Error.Code);
                return;
            }

            ShopeeOrderList page = pageResult.Value;
            foreach (string orderSn in page.OrderSns)
            {
                Result ingestResult =
                    await ingestionService.IngestOrderAsync(connection, orderSn, cancellationToken);
                if (ingestResult.IsFailure)
                {
                    logger.LogWarning(
                        "Shopee order {OrderSn} reconciliation ingest failed for tenant {TenantId}: {ErrorCode}",
                        orderSn, connection.TenantId, ingestResult.Error.Code);
                }
            }

            cursor = page.HasMore ? page.NextCursor : null;
        } while (!string.IsNullOrEmpty(cursor));
    }

    private async Task RetryStuckTrackingAsync(
        int trackingRetryThresholdMinutes, CancellationToken cancellationToken)
    {
        DateTime arrangedBefore = DateTime.UtcNow.AddMinutes(-trackingRetryThresholdMinutes);
        IReadOnlyList<ShopeeOrder> stuckOrders = await orderRepository.ListAwaitingTrackingAsync(
            arrangedBefore, TrackingRetryBatchSize, cancellationToken);

        foreach (ShopeeOrder order in stuckOrders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ShopeeShopConnection? connection =
                await connectionRepository.GetByTenantIdAsync(order.TenantId, cancellationToken);
            if (connection is null)
            {
                continue;
            }

            await tokenRefresher.RefreshIfNeededAsync(connection, cancellationToken);
            Result<string?> trackingResult = await shopeeGateway.GetTrackingNumberAsync(
                connection.ShopId, connection.AccessToken, order.OrderSn, cancellationToken);
            if (trackingResult.IsFailure || string.IsNullOrWhiteSpace(trackingResult.Value))
            {
                continue;
            }

            Result completeResult = await completionService.CompleteAsync(
                order, trackingResult.Value, cancellationToken);
            if (completeResult.IsFailure)
            {
                logger.LogWarning(
                    "Shopee order {OrderSn} tracking completion failed: {ErrorCode}",
                    order.OrderSn, completeResult.Error.Code);
            }
        }
    }
}
```

`ShopeeOrderReconciliationJob.cs` — identical shape to `ShopeeWebhookDispatchJob` but with `PeriodicTimer(TimeSpan.FromMinutes(Math.Max(syncOptions.ReconciliationIntervalMinutes, 1)))` and calling `processor.RunAsync(syncOptions.WindowHours, syncOptions.TrackingRetryThresholdMinutes, cancellationToken)`. Gate on the same `OrderSync.Enabled` flag. Register both.

- [ ] **Step 4: Run tests, build** — PASS + clean build.

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "feat(shopee): hourly order reconciliation and stuck-tracking re-poll"
```

---

### Task 9: Ship flow — shipping-parameter query, ship command, relink command

**Files:**
- Create: `api/src/Wrapsfer.Application/Shopee/Queries/GetShopeeShippingParameter/GetShopeeShippingParameterQuery.cs` + `GetShopeeShippingParameterQueryHandler.cs`
- Create: `api/src/Wrapsfer.Application/Shopee/Commands/ShipShopeeOrder/ShipShopeeOrderCommand.cs` + `ShipShopeeOrderCommandValidator.cs` + `ShipShopeeOrderCommandHandler.cs`
- Create: `api/src/Wrapsfer.Application/Shopee/Commands/RelinkShopeeOrderItems/RelinkShopeeOrderItemsCommand.cs` + `RelinkShopeeOrderItemsCommandHandler.cs`
- Create (Task 10 defines the response records — this task can be done after Task 10 if preferred; if done first, create the response records here as specified in Task 10)
- Test: `api/tests/Wrapsfer.Application.Tests/Shopee/ShipShopeeOrderCommandHandlerTests.cs`

**Interfaces:**
- Produces:
  - `record GetShopeeShippingParameterQuery(string TenantId, Guid OrderId) : IQuery<ShopeeShippingParameterResponse>`
  - `record ShipShopeeOrderCommand(string TenantId, Guid OrderId, string Method, long? AddressId, string? PickupTimeId, long? BranchId) : ICommand<ShopeeOrderResponse>` — `Method` is `"pickup"` or `"dropoff"`
  - `record RelinkShopeeOrderItemsCommand(string TenantId, Guid OrderId) : ICommand<ShopeeOrderResponse>`

- [ ] **Step 1: Write failing ship-handler tests** — mocks as before:
  1. Happy path (dropoff): order `ReadyToShip`, connection found, `ShipOrderAsync` success, `GetTrackingNumberAsync` returns null → order ends `AwaitingTracking`, success response.
  2. Immediate tracking: `GetTrackingNumberAsync` returns `"TRACK1"` → completion service invoked, response reflects `Shipped`.
  3. `ShipOrderAsync` failure → order `ShipmentFailed`, `LastShipError` set, command returns `ShopeeOrderErrors.ShipmentRequestFailed`.
  4. Order in `NeedsLinking` → `ShopeeOrderErrors.NotReadyToShip`, gateway never called.
  5. Pickup without `AddressId`/`PickupTimeId` → validator failure (test the validator directly with `TestValidate` like existing validator tests — find one with `grep -rn "TestValidate" api/tests/Wrapsfer.Application.Tests | head -3` and mirror it).

- [ ] **Step 2: Run to verify failure.**

- [ ] **Step 3: Implement**

`ShipShopeeOrderCommand.cs`:

```csharp
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Responses;

namespace Wrapsfer.Application.Shopee.Commands.ShipShopeeOrder;

public sealed record ShipShopeeOrderCommand(
    string TenantId,
    Guid OrderId,
    string Method,
    long? AddressId,
    string? PickupTimeId,
    long? BranchId) : ICommand<ShopeeOrderResponse>;
```

`ShipShopeeOrderCommandValidator.cs`:

```csharp
using FluentValidation;

namespace Wrapsfer.Application.Shopee.Commands.ShipShopeeOrder;

internal sealed class ShipShopeeOrderCommandValidator : AbstractValidator<ShipShopeeOrderCommand>
{
    public ShipShopeeOrderCommandValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.Method)
            .Must(m => m is "pickup" or "dropoff")
            .WithMessage("Method must be pickup or dropoff");
        When(c => c.Method == "pickup", () =>
        {
            RuleFor(c => c.AddressId).NotNull();
            RuleFor(c => c.PickupTimeId).NotEmpty();
        });
    }
}
```

`ShipShopeeOrderCommandHandler.cs`:

```csharp
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Commands.ShipShopeeOrder;

internal sealed class ShipShopeeOrderCommandHandler(
    IShopeeOrderRepository orderRepository,
    IShopeeShopConnectionRepository connectionRepository,
    IShopeeGateway shopeeGateway,
    ShopeeConnectionTokenRefresher tokenRefresher,
    ShopeeOrderShipmentCompletionService completionService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<ShipShopeeOrderCommand, ShopeeOrderResponse>
{
    public async Task<Result<ShopeeOrderResponse>> Handle(
        ShipShopeeOrderCommand request, CancellationToken cancellationToken)
    {
        ShopeeOrder? order = await orderRepository.GetByIdAsync(
            request.OrderId, request.TenantId, cancellationToken);
        if (order is null)
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.NotFound);
        }

        if (order.Status is not (ShopeeOrderStatus.ReadyToShip or ShopeeOrderStatus.ShipmentFailed))
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.NotReadyToShip);
        }

        if (order.HasUnresolvedItems)
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.ItemsNotLinked);
        }

        // Spec: IN_CANCEL (buyer requested cancellation, unresolved on Shopee) blocks
        // arranging new shipment but cancels nothing.
        if (string.Equals(order.ShopeeStatus, "IN_CANCEL", StringComparison.Ordinal))
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.CancellationRequested);
        }

        ShopeeShopConnection? connection = await connectionRepository.GetByTenantIdAsync(
            request.TenantId, cancellationToken);
        if (connection is null)
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.ConnectionNotFound);
        }

        await tokenRefresher.RefreshIfNeededAsync(connection, cancellationToken);

        ShopeeShipOrderRequest shipRequest = request.Method == "pickup"
            ? new ShopeeShipOrderRequest(
                order.OrderSn, new ShopeeShipOrderPickup(request.AddressId!.Value, request.PickupTimeId!), null)
            : new ShopeeShipOrderRequest(
                order.OrderSn, null, new ShopeeShipOrderDropoff(request.BranchId));

        Result shipResult = await shopeeGateway.ShipOrderAsync(
            connection.ShopId, connection.AccessToken, shipRequest, cancellationToken);
        if (shipResult.IsFailure)
        {
            Result failResult = order.MarkShipmentFailed(shipResult.Error.Description, DateTime.UtcNow);
            if (failResult.IsSuccess)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.ShipmentRequestFailed);
        }

        Result arrangeResult = order.MarkShipmentArranged(tenantContext.Username, DateTime.UtcNow);
        if (arrangeResult.IsFailure)
        {
            return Result<ShopeeOrderResponse>.Failure(arrangeResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Opportunistic: Shopee often assigns the tracking number within seconds. A failure
        // here is not an error — the tracking push or reconciliation completes it later.
        Result<string?> trackingResult = await shopeeGateway.GetTrackingNumberAsync(
            connection.ShopId, connection.AccessToken, order.OrderSn, cancellationToken);
        if (trackingResult.IsSuccess && !string.IsNullOrWhiteSpace(trackingResult.Value))
        {
            await completionService.CompleteAsync(order, trackingResult.Value, cancellationToken);
        }

        return Result<ShopeeOrderResponse>.Success(ShopeeOrderResponseMapper.Map(order));
    }
}
```

`GetShopeeShippingParameterQueryHandler.cs` — load order (must exist and be `ReadyToShip`/`ShipmentFailed`, else `ShopeeOrderErrors.NotReadyToShip`), load connection, refresh tokens, call `GetShippingParameterAsync`, map to `ShopeeShippingParameterResponse` (record mirrors the gateway record shapes — see Task 10). `RelinkShopeeOrderItemsCommandHandler` — load order (`NotFound` if missing), load connection (`ConnectionNotFound`), call `ingestionService.IngestOrderAsync(connection, order.OrderSn, ct)`, reload via `GetByIdAsync`, return mapped response.

- [ ] **Step 4: Run tests** — PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "feat(shopee): user-triggered shipment arrangement with immediate tracking attempt"
```

---

### Task 10: Read side — list/detail queries, responses, label query

**Files:**
- Create in `api/src/Wrapsfer.Application/Shopee/Responses/`: `ShopeeOrderResponse.cs`, `ShopeeOrderItemResponse.cs`, `ShopeeOrdersResponse.cs`, `ShopeeShippingParameterResponse.cs`, `ShopeePickupAddressResponse.cs`, `ShopeePickupTimeSlotResponse.cs`, `ShopeeDropoffBranchResponse.cs`, `ShopeeOrderLabelResult.cs`
- Create: `api/src/Wrapsfer.Application/Shopee/Common/ShopeeOrderResponseMapper.cs`
- Create: `api/src/Wrapsfer.Application/Shopee/Queries/GetShopeeOrders/GetShopeeOrdersQuery.cs` + handler
- Create: `api/src/Wrapsfer.Application/Shopee/Queries/GetShopeeOrderDetail/GetShopeeOrderDetailQuery.cs` + handler
- Create: `api/src/Wrapsfer.Application/Shopee/Queries/GetShopeeOrderLabel/GetShopeeOrderLabelQuery.cs` + handler
- Modify: `api/src/Wrapsfer.Application/Abstractions/Storage/IReportStorage.cs` (add `DownloadAsync`)
- Modify: `api/src/Wrapsfer.Infrastructure/Storage/S3ReportStorage.cs` (implement `DownloadAsync`)
- Test: `api/tests/Wrapsfer.Application.Tests/Shopee/GetShopeeOrderLabelQueryHandlerTests.cs`

**Interfaces:**
- Produces:

```csharp
public sealed record ShopeeOrderItemResponse(Guid Id, long ShopeeItemId, long ShopeeModelId,
    string? ItemName, string? ModelName, string? ItemSku, int Quantity, Guid? ProductId);
public sealed record ShopeeOrderResponse(Guid Id, string OrderSn, string ShopeeStatus, string Status,
    string? BuyerUsername, string? RecipientName, decimal TotalAmount, string? Currency,
    string? ShippingCarrier, DateTime? ShipByDate, string? TrackingNumber, Guid? WaybillId,
    DateTime? ShipmentArrangedAt, DateTime? LabelPrintedAt, DateTime? CancelledAt,
    string? LastShipError, IReadOnlyList<ShopeeOrderItemResponse> Items);
public sealed record ShopeeOrdersResponse(IReadOnlyList<ShopeeOrderResponse> Items,
    int TotalCount, int Page, int PageSize);
public sealed record ShopeePickupTimeSlotResponse(string PickupTimeId, DateTime Date, string? TimeText);
public sealed record ShopeePickupAddressResponse(long AddressId, string Address,
    IReadOnlyList<ShopeePickupTimeSlotResponse> TimeSlots);
public sealed record ShopeeDropoffBranchResponse(long BranchId, string Address);
public sealed record ShopeeShippingParameterResponse(bool SupportsPickup, bool SupportsDropoff,
    IReadOnlyList<ShopeePickupAddressResponse> PickupAddresses,
    IReadOnlyList<ShopeeDropoffBranchResponse> DropoffBranches);
public sealed record ShopeeOrderLabelResult(byte[] Content, string ContentType, string FileName);

public sealed record GetShopeeOrdersQuery(string TenantId, ShopeeOrderStatus? Status,
    string? Search, int Page, int PageSize) : IQuery<ShopeeOrdersResponse>;
public sealed record GetShopeeOrderDetailQuery(string TenantId, Guid OrderId) : IQuery<ShopeeOrderResponse>;
public sealed record GetShopeeOrderLabelQuery(string TenantId, Guid OrderId) : IQuery<ShopeeOrderLabelResult>;

// mapper: internal static class ShopeeOrderResponseMapper { internal static ShopeeOrderResponse Map(ShopeeOrder order); }
// Status maps via order.Status.ToString() ("NeedsLinking", "ReadyToShip", ...) — the frontend consumes these strings.
```
  - `IReportStorage` addition:

```csharp
    /// <summary>
    /// Downloads a previously uploaded object's bytes. Returns null when the object
    /// does not exist (caller re-fetches from the source and re-uploads).
    /// </summary>
    Task<byte[]?> DownloadAsync(string objectKey, CancellationToken cancellationToken = default);
```

- [ ] **Step 1: Write failing label-handler tests:**
  1. Order not `Shipped` → `ShopeeOrderErrors.NotShipped`.
  2. `LabelStorageKey` set + storage returns bytes → result served from storage, gateway never called, no save.
  3. No stored label → gateway `DownloadShippingDocumentAsync` returns bytes, `UploadAsync` called with key `shopee-labels/{tenantId}/{orderSn}.pdf` and content type `application/pdf`, order gets `LabelStorageKey` (from `StoredReport.ObjectKey`) + `LabelPrintedAt`, saved, bytes returned, `FileName == $"shopee-awb-{orderSn}.pdf"`.
  4. Stored key but storage returns null (object expired/deleted) → falls through to gateway re-fetch + re-upload.

- [ ] **Step 2: Run to verify failure.**

- [ ] **Step 3: Implement**

S3 `DownloadAsync` (in `S3ReportStorage.cs`, using the same key convention note as `DeleteAsync` — the persisted key is already fully prefixed, do not re-apply `BuildKey`):

```csharp
    public async Task<byte[]?> DownloadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BucketName))
        {
            throw new InvalidOperationException(
                $"{ReportStorageOptions.SectionName}:BucketName is not configured; cannot download reports.");
        }

        try
        {
            GetObjectRequest request = new()
            {
                BucketName = _options.BucketName,
                Key = objectKey
            };

            using GetObjectResponse response = await s3Client.GetObjectAsync(request, cancellationToken);
            using MemoryStream buffer = new();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
```

`GetShopeeOrderLabelQueryHandler.cs`:

```csharp
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Abstractions.Storage;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Queries.GetShopeeOrderLabel;

internal sealed class GetShopeeOrderLabelQueryHandler(
    IShopeeOrderRepository orderRepository,
    IShopeeShopConnectionRepository connectionRepository,
    IShopeeGateway shopeeGateway,
    ShopeeConnectionTokenRefresher tokenRefresher,
    IReportStorage reportStorage,
    IUnitOfWork unitOfWork) : IQueryHandler<GetShopeeOrderLabelQuery, ShopeeOrderLabelResult>
{
    private const string PdfContentType = "application/pdf";

    public async Task<Result<ShopeeOrderLabelResult>> Handle(
        GetShopeeOrderLabelQuery request, CancellationToken cancellationToken)
    {
        ShopeeOrder? order = await orderRepository.GetByIdAsync(
            request.OrderId, request.TenantId, cancellationToken);
        if (order is null)
        {
            return Result<ShopeeOrderLabelResult>.Failure(ShopeeOrderErrors.NotFound);
        }

        if (order.Status != ShopeeOrderStatus.Shipped)
        {
            return Result<ShopeeOrderLabelResult>.Failure(ShopeeOrderErrors.NotShipped);
        }

        string fileName = $"shopee-awb-{order.OrderSn}.pdf";

        if (!string.IsNullOrEmpty(order.LabelStorageKey))
        {
            byte[]? stored = await reportStorage.DownloadAsync(order.LabelStorageKey, cancellationToken);
            if (stored is not null)
            {
                return Result<ShopeeOrderLabelResult>.Success(
                    new ShopeeOrderLabelResult(stored, PdfContentType, fileName));
            }
        }

        ShopeeShopConnection? connection = await connectionRepository.GetByTenantIdAsync(
            request.TenantId, cancellationToken);
        if (connection is null)
        {
            return Result<ShopeeOrderLabelResult>.Failure(ShopeeOrderErrors.ConnectionNotFound);
        }

        await tokenRefresher.RefreshIfNeededAsync(connection, cancellationToken);
        Result<byte[]> documentResult = await shopeeGateway.DownloadShippingDocumentAsync(
            connection.ShopId, connection.AccessToken, order.OrderSn, cancellationToken);
        if (documentResult.IsFailure)
        {
            return Result<ShopeeOrderLabelResult>.Failure(documentResult.Error);
        }

        StoredReport storedReport = await reportStorage.UploadAsync(
            $"shopee-labels/{order.TenantId}/{order.OrderSn}.pdf",
            documentResult.Value, PdfContentType, cancellationToken);

        order.MarkLabelStored(storedReport.ObjectKey);
        order.MarkLabelPrinted(DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ShopeeOrderLabelResult>.Success(
            new ShopeeOrderLabelResult(documentResult.Value, PdfContentType, fileName));
    }
}
```

`GetShopeeOrdersQueryHandler` — call `orderRepository.ListAsync(request.TenantId, request.Status, request.Search, request.Page, request.PageSize, ct)` and map each with `ShopeeOrderResponseMapper.Map`. `GetShopeeOrderDetailQueryHandler` — `GetByIdAsync` + map, `NotFound` when missing. Mapper maps every `ShopeeOrderResponse` field 1:1 from the entity, `Status = order.Status.ToString()`, items ordered by `ShopeeItemId`.

- [ ] **Step 4: Run tests + full build** — `dotnet test --filter "FullyQualifiedName~GetShopeeOrderLabel"` PASS; `make build` clean.

- [ ] **Step 5: Commit**

```bash
git add src/ tests/
git commit -m "feat(shopee): order queries and persisted AWB label download"
```

---

### Task 11: API surface — controller endpoints + contracts

**Files:**
- Modify: `api/src/Wrapsfer.Api/Controllers/V1/ShopeeController.cs`
- Create: `api/src/Wrapsfer.Api/Contracts/ShipShopeeOrderRequest.cs`

**Interfaces:**
- Produces the HTTP surface the frontend consumes (all under the existing `[Authorize(Policy = PartnerIntegrationAdmin)]` except the webhook added in Task 7):
  - `GET api/v1/shopee/{tenantId}/orders?status=&search=&page=&pageSize=` → `ShopeeOrdersResponse`
  - `GET api/v1/shopee/{tenantId}/orders/{orderId:guid}` → `ShopeeOrderResponse`
  - `GET api/v1/shopee/{tenantId}/orders/{orderId:guid}/shipping-parameter` → `ShopeeShippingParameterResponse`
  - `POST api/v1/shopee/{tenantId}/orders/{orderId:guid}/ship` body `ShipShopeeOrderRequest(string Method, long? AddressId, string? PickupTimeId, long? BranchId)` → `ShopeeOrderResponse`
  - `POST api/v1/shopee/{tenantId}/orders/{orderId:guid}/relink` → `ShopeeOrderResponse`
  - `GET api/v1/shopee/{tenantId}/orders/{orderId:guid}/label` → `File(content, "application/pdf", fileName)`

- [ ] **Step 1: Add the contract and actions**

`ShipShopeeOrderRequest.cs`:

```csharp
namespace Wrapsfer.Api.Contracts;

public sealed record ShipShopeeOrderRequest(
    string Method,
    long? AddressId,
    string? PickupTimeId,
    long? BranchId);
```

Controller actions (append to `ShopeeController`, matching the file's existing style):

```csharp
    [HttpGet("{tenantId}/orders")]
    public async Task<IActionResult> ListOrders(
        string tenantId,
        [FromQuery] ShopeeOrderStatus? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        Result<ShopeeOrdersResponse> result = await sender.Send(
            new GetShopeeOrdersQuery(tenantId, status, search, Math.Max(page, 1),
                Math.Clamp(pageSize, 1, 100)),
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{tenantId}/orders/{orderId:guid}")]
    public async Task<IActionResult> GetOrder(
        string tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        Result<ShopeeOrderResponse> result = await sender.Send(
            new GetShopeeOrderDetailQuery(tenantId, orderId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{tenantId}/orders/{orderId:guid}/shipping-parameter")]
    public async Task<IActionResult> GetShippingParameter(
        string tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        Result<ShopeeShippingParameterResponse> result = await sender.Send(
            new GetShopeeShippingParameterQuery(tenantId, orderId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tenantId}/orders/{orderId:guid}/ship")]
    public async Task<IActionResult> ShipOrder(
        string tenantId,
        Guid orderId,
        [FromBody] ShipShopeeOrderRequest request,
        CancellationToken cancellationToken)
    {
        Result<ShopeeOrderResponse> result = await sender.Send(
            new ShipShopeeOrderCommand(
                tenantId, orderId, request.Method, request.AddressId,
                request.PickupTimeId, request.BranchId),
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{tenantId}/orders/{orderId:guid}/relink")]
    public async Task<IActionResult> RelinkOrder(
        string tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        Result<ShopeeOrderResponse> result = await sender.Send(
            new RelinkShopeeOrderItemsCommand(tenantId, orderId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{tenantId}/orders/{orderId:guid}/label")]
    public async Task<IActionResult> DownloadOrderLabel(
        string tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        Result<ShopeeOrderLabelResult> result = await sender.Send(
            new GetShopeeOrderLabelQuery(tenantId, orderId), cancellationToken);
        if (result.IsFailure)
        {
            return ToActionResult(result);
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }
```

Note: if `ToActionResult(result)` on a failed `Result<ShopeeOrderLabelResult>` doesn't compile that way, follow whatever the closest existing file-returning action does (`WaybillsController.DownloadExportFile` at line ~307 is the reference).

- [ ] **Step 2: Build + run all tests** — `make build && make test` → clean.

- [ ] **Step 3: Commit**

```bash
git add src/
git commit -m "feat(shopee): order endpoints — list, detail, ship, relink, label"
```

Backend is now feature-complete. Update `Shopee` config docs: add `Shopee:PushCallbackUrl` and `Shopee:OrderSync:*` keys wherever `appsettings`/Infisical config for `Shopee:StockSync` is documented (grep `appsettings*.json` for `"StockSync"` and add siblings; also register the push URL + enable order push codes in the Shopee Open Platform console — manual step, note it in the PR description).

---

### Task 12: Frontend — promote product-linking drawers to shared components

All remaining tasks are in the `partner/` repo (its own git repo — commit there).

The spec calls for reusing the link/create-product drawers from `shopee-products` inside the new `shopee-orders` feature, but `partner/CLAUDE.md` forbids cross-feature imports and sanctions promotion to `src/components/` when UI is used by ≥2 features. So: move the drawers (and only what they need) to `src/components/shopee-linking/`, and have `shopee-products` consume them from there.

**Files:**
- Move: `partner/src/features/partner/shopee-products/components/link-product-drawer.tsx` → `partner/src/components/shopee-linking/link-product-drawer.tsx`
- Move: `partner/src/features/partner/shopee-products/components/create-linked-product-drawer.tsx` → `partner/src/components/shopee-linking/create-linked-product-drawer.tsx`
- Move: `partner/src/features/partner/shopee-products/components/create-linked-product-form.tsx` → `partner/src/components/shopee-linking/create-linked-product-form.tsx`
- Create: `partner/src/components/shopee-linking/service.ts` — move `searchLinkableProducts`, `linkShopeeProduct`, `createShopeeLinkedProduct` here from the feature's `service.ts` (keep `shopeePath` as a local helper copy); the feature's `service.ts` re-exports them (`export { linkShopeeProduct, createShopeeLinkedProduct, searchLinkableProducts } from "@/components/shopee-linking/service";`) so existing call sites keep working.
- Create: `partner/src/components/shopee-linking/types.ts` — move the types those three functions and the drawers need (`Product`, `PagedResult`, `LinkShopeeProductPayload`, `CreateShopeeLinkedProductPayload`, `ShopeeProductLink`); the feature's `types.ts` re-exports them.
- Move any schema the create form uses (check its imports) into the shared folder alongside it.

- [ ] **Step 1: Move the files and rewrite imports**

```bash
cd partner
mkdir -p src/components/shopee-linking
git mv src/features/partner/shopee-products/components/link-product-drawer.tsx src/components/shopee-linking/
git mv src/features/partner/shopee-products/components/create-linked-product-drawer.tsx src/components/shopee-linking/
git mv src/features/partner/shopee-products/components/create-linked-product-form.tsx src/components/shopee-linking/
```

Then, in the moved files and the feature: replace every `@/features/partner/shopee-products/service` import of the three moved functions with `@/components/shopee-linking/service`, and the moved types with `@/components/shopee-linking/types`. Update `shopee-products` components that render the drawers to import from `@/components/shopee-linking/...`. Read each moved file top-to-bottom — every one of its imports must resolve to either the shared folder or global infrastructure (`@/lib/...`), never back into `features/`.

- [ ] **Step 2: Verify nothing broke**

```bash
pnpm typecheck && pnpm test && pnpm check
```

Expected: zero type errors; existing tests pass; Biome clean.

- [ ] **Step 3: Commit (partner repo)**

```bash
git add -A
git commit -m "refactor(shopee): promote product-linking drawers to shared components"
```

---

### Task 13: Frontend — shopee-orders feature: types, service, query options, route, table

**Files:**
- Create: `partner/src/features/partner/shopee-orders/types.ts`
- Create: `partner/src/features/partner/shopee-orders/service.ts`
- Create: `partner/src/features/partner/shopee-orders/query-options.ts`
- Create: `partner/src/features/partner/shopee-orders/index.tsx` (`ShopeeOrdersPage`)
- Create: `partner/src/features/partner/shopee-orders/components/shopee-orders-table.tsx`
- Create: `partner/src/features/partner/shopee-orders/components/order-status-badge.tsx`
- Create: `partner/src/features/partner/shopee-orders/components/shopee-orders-empty-state.tsx`
- Create: `partner/src/routes/partner/_layout/shopee-orders.tsx`
- Modify: the nav — find it with `grep -rn "shopee-products" partner/src --include="*.tsx" -l | grep -v features/partner/shopee-products | grep -v routes/` and add a sibling "Shopee Orders" entry pointing at `/partner/shopee-orders` (icon: `PackageCheck` from lucide-react).
- Test: `partner/src/features/partner/shopee-orders/components/order-status-badge.test.tsx`

**Interfaces:**
- Produces (Task 14 consumes): everything in `types.ts`/`service.ts` below, `shopeeOrdersQueryOptions`, and the `ShopeeOrdersTable` props.

- [ ] **Step 1: Write `types.ts`**

```ts
export type ShopeeOrderStatus =
	| "NeedsLinking"
	| "ReadyToShip"
	| "AwaitingTracking"
	| "Shipped"
	| "Cancelled"
	| "ShipmentFailed";

export interface ShopeeOrderItem {
	id: string;
	shopeeItemId: number;
	shopeeModelId: number;
	itemName: string | null;
	modelName: string | null;
	itemSku: string | null;
	quantity: number;
	productId: string | null;
}

export interface ShopeeOrder {
	id: string;
	orderSn: string;
	shopeeStatus: string;
	status: ShopeeOrderStatus;
	buyerUsername: string | null;
	recipientName: string | null;
	totalAmount: number;
	currency: string | null;
	shippingCarrier: string | null;
	shipByDate: string | null;
	trackingNumber: string | null;
	waybillId: string | null;
	shipmentArrangedAt: string | null;
	labelPrintedAt: string | null;
	cancelledAt: string | null;
	lastShipError: string | null;
	items: ShopeeOrderItem[];
}

export interface ShopeeOrdersPage {
	items: ShopeeOrder[];
	totalCount: number;
	page: number;
	pageSize: number;
}

export interface ShopeePickupTimeSlot {
	pickupTimeId: string;
	date: string;
	timeText: string | null;
}

export interface ShopeePickupAddress {
	addressId: number;
	address: string;
	timeSlots: ShopeePickupTimeSlot[];
}

export interface ShopeeDropoffBranch {
	branchId: number;
	address: string;
}

export interface ShopeeShippingParameter {
	supportsPickup: boolean;
	supportsDropoff: boolean;
	pickupAddresses: ShopeePickupAddress[];
	dropoffBranches: ShopeeDropoffBranch[];
}

export interface ShipShopeeOrderPayload {
	method: "pickup" | "dropoff";
	addressId?: number;
	pickupTimeId?: string;
	branchId?: number;
}

export interface ShopeeOrdersFilter {
	status?: ShopeeOrderStatus;
	search?: string;
	page: number;
	pageSize: number;
}
```

- [ ] **Step 2: Write `service.ts`**

```ts
import type {
	ShipShopeeOrderPayload,
	ShopeeOrder,
	ShopeeOrdersFilter,
	ShopeeOrdersPage,
	ShopeeShippingParameter,
} from "@/features/partner/shopee-orders/types";
import { api } from "@/lib/api";

function ordersPath(tenantId: string): string {
	return `/api/v1/shopee/${encodeURIComponent(tenantId)}/orders`;
}

export async function listShopeeOrders(
	tenantId: string,
	filter: ShopeeOrdersFilter,
): Promise<ShopeeOrdersPage> {
	const searchParams = new URLSearchParams({
		page: String(filter.page),
		pageSize: String(filter.pageSize),
	});
	if (filter.status) {
		searchParams.set("status", filter.status);
	}
	if (filter.search && filter.search.trim().length > 0) {
		searchParams.set("search", filter.search.trim());
	}
	const { data } = await api.get<ShopeeOrdersPage>(
		`${ordersPath(tenantId)}?${searchParams.toString()}`,
	);
	return data;
}

export async function getShopeeOrder(
	tenantId: string,
	orderId: string,
): Promise<ShopeeOrder> {
	const { data } = await api.get<ShopeeOrder>(`${ordersPath(tenantId)}/${orderId}`);
	return data;
}

export async function getShippingParameter(
	tenantId: string,
	orderId: string,
): Promise<ShopeeShippingParameter> {
	const { data } = await api.get<ShopeeShippingParameter>(
		`${ordersPath(tenantId)}/${orderId}/shipping-parameter`,
	);
	return data;
}

export async function shipShopeeOrder(
	tenantId: string,
	orderId: string,
	payload: ShipShopeeOrderPayload,
): Promise<ShopeeOrder> {
	const { data } = await api.post<ShopeeOrder>(
		`${ordersPath(tenantId)}/${orderId}/ship`,
		payload,
	);
	return data;
}

export async function relinkShopeeOrder(
	tenantId: string,
	orderId: string,
): Promise<ShopeeOrder> {
	const { data } = await api.post<ShopeeOrder>(
		`${ordersPath(tenantId)}/${orderId}/relink`,
	);
	return data;
}

export async function downloadShopeeOrderLabel(
	tenantId: string,
	orderId: string,
	orderSn: string,
): Promise<void> {
	// The api wrapper is JSON-oriented; the label is a PDF, so use fetch directly.
	// Session auth rides on httpOnly cookies (credentials: include), same as the wrapper.
	const response = await fetch(`${ordersPath(tenantId)}/${orderId}/label`, {
		credentials: "include",
	});
	if (!response.ok) {
		throw new Error(`Label download failed with status ${response.status}`);
	}
	const blob = await response.blob();
	const url = URL.createObjectURL(blob);
	const anchor = document.createElement("a");
	anchor.href = url;
	anchor.download = `shopee-awb-${orderSn}.pdf`;
	document.body.appendChild(anchor);
	anchor.click();
	anchor.remove();
	URL.revokeObjectURL(url);
}
```

Note: if `VITE_API_BASE_URL` is used for deployed environments, check how `src/lib/api.ts` builds absolute URLs and reuse the same base-URL helper in `downloadShopeeOrderLabel` instead of a bare relative path.

- [ ] **Step 3: Write `query-options.ts`**

```ts
import { queryOptions } from "@tanstack/react-query";
import { listShopeeOrders } from "@/features/partner/shopee-orders/service";
import type { ShopeeOrdersFilter } from "@/features/partner/shopee-orders/types";

export const shopeeOrdersQueryOptions = (
	tenantId: string,
	filter: ShopeeOrdersFilter,
) =>
	queryOptions({
		queryKey: ["shopee-orders", tenantId, filter],
		queryFn: () => listShopeeOrders(tenantId, filter),
	});
```

- [ ] **Step 4: Write the badge component + its test (test first)**

`order-status-badge.test.tsx`:

```tsx
import { MantineProvider } from "@mantine/core";
import { render, screen } from "@testing-library/react";
import { IntlProvider } from "react-intl";
import { describe, expect, it } from "vitest";
import { OrderStatusBadge } from "@/features/partner/shopee-orders/components/order-status-badge";

function renderBadge(status: Parameters<typeof OrderStatusBadge>[0]["status"]) {
	return render(
		<IntlProvider locale="en" onError={() => {}}>
			<MantineProvider>
				<OrderStatusBadge status={status} />
			</MantineProvider>
		</IntlProvider>,
	);
}

describe("OrderStatusBadge", () => {
	it("renders needs-linking state", () => {
		renderBadge("NeedsLinking");
		expect(screen.getByText("Needs linking")).toBeInTheDocument();
	});

	it("renders shipment-failed state", () => {
		renderBadge("ShipmentFailed");
		expect(screen.getByText("Shipment failed")).toBeInTheDocument();
	});
});
```

Run `pnpm test -- order-status-badge` → FAIL (component missing). Then implement `order-status-badge.tsx`:

```tsx
import { Badge } from "@mantine/core";
import { FormattedMessage } from "react-intl";
import type { ShopeeOrderStatus } from "@/features/partner/shopee-orders/types";

const statusConfig: Record<ShopeeOrderStatus, { color: string; id: string; defaultMessage: string }> = {
	NeedsLinking: {
		color: "yellow",
		id: "partner.shopeeOrders.status.needsLinking",
		defaultMessage: "Needs linking",
	},
	ReadyToShip: {
		color: "teal",
		id: "partner.shopeeOrders.status.readyToShip",
		defaultMessage: "Ready to ship",
	},
	AwaitingTracking: {
		color: "blue",
		id: "partner.shopeeOrders.status.awaitingTracking",
		defaultMessage: "Arranging",
	},
	Shipped: {
		color: "green",
		id: "partner.shopeeOrders.status.shipped",
		defaultMessage: "Shipped",
	},
	Cancelled: {
		color: "gray",
		id: "partner.shopeeOrders.status.cancelled",
		defaultMessage: "Cancelled",
	},
	ShipmentFailed: {
		color: "red",
		id: "partner.shopeeOrders.status.shipmentFailed",
		defaultMessage: "Shipment failed",
	},
};

interface OrderStatusBadgeProps {
	status: ShopeeOrderStatus;
}

export function OrderStatusBadge({ status }: OrderStatusBadgeProps) {
	const config = statusConfig[status];
	return (
		<Badge color={config.color} variant="light">
			<FormattedMessage id={config.id} defaultMessage={config.defaultMessage} />
		</Badge>
	);
}
```

Run `pnpm test -- order-status-badge` → PASS.

- [ ] **Step 5: Build the page, table, empty state, and route**

`index.tsx` — `ShopeeOrdersPage({ tenantId }: { tenantId: string })`: `<Container fluid px="xl" py="xl">`, `<Title order={2}>` "Shopee Orders", a `Tabs` control whose values are `all | NeedsLinking | ReadyToShip | AwaitingTracking | Shipped | Cancelled` (tab labels via `FormattedMessage`, ids `partner.shopeeOrders.tabs.*`), a search `TextInput` (debounced 300ms via `useDebouncedValue` from `@mantine/hooks`), and `<ShopeeOrdersTable>`. State: `status`, `search`, `page` via `useState`; data via `useQuery(shopeeOrdersQueryOptions(tenantId, { status, search, page, pageSize: 20 }))` with `refetchInterval: (query) => query.state.data?.items.some((o) => o.status === "AwaitingTracking") ? 10_000 : false` so arranging orders flip to Shipped without manual refresh. `Pagination` under the table using `totalCount`.

`shopee-orders-table.tsx` — props:

```tsx
interface ShopeeOrdersTableProps {
	tenantId: string;
	orders: ShopeeOrder[];
	onArrangeShipment: (order: ShopeeOrder) => void;
	onOrderChanged: () => void; // invalidate the list query
}
```

Columns: order SN (`<Text lineClamp={1}>`), buyer, items summary (`{count} items`, expandable row listing each item name × quantity with a yellow `AlertTriangle` icon on unresolved items), ship-by date (`FormattedDate`), courier, tracking number (or `—`), `OrderStatusBadge`, printed indicator (`Printer` icon with `aria-label` when `labelPrintedAt` set), and a per-row actions `Group`:
- `NeedsLinking` → "Link products" `Button size="sm"` (wired in Task 14) + "Re-check" `ActionIcon` calling `relinkShopeeOrder` then `onOrderChanged()`.
- `ReadyToShip`/`ShipmentFailed` → "Arrange shipment" `Button size="sm"` → `onArrangeShipment(order)`; for `ShipmentFailed` also show `lastShipError` in a `Tooltip` on a red `AlertCircle` icon.
- `Shipped` → "Label" `Button size="sm" variant="light"` calling `downloadShopeeOrderLabel` then `onOrderChanged()`.
- `shopeeStatus === "IN_CANCEL"` → yellow warning `Badge` "Cancellation requested" next to the status badge.
- `Cancelled` with `waybillId` → gray text note "Waybill cancelled/needs attention".

All copy via `FormattedMessage`/`useIntl` with ids under `partner.shopeeOrders.table.*`. Empty state per the design conventions (icon `ShoppingCart` 48px, `py={64}`).

Route `partner/src/routes/partner/_layout/shopee-orders.tsx` (mirror `shopee-products.tsx`):

```tsx
import { createFileRoute } from "@tanstack/react-router";
import { meQueryOptions } from "@/features/auth/query-options";
import { ShopeeOrdersPage } from "@/features/partner/shopee-orders";
import { shopeeOrdersQueryOptions } from "@/features/partner/shopee-orders/query-options";

export const Route = createFileRoute("/partner/_layout/shopee-orders")({
	loader: async ({ context }) => {
		const me = await context.queryClient.ensureQueryData(meQueryOptions());
		await context.queryClient
			.ensureQueryData(
				shopeeOrdersQueryOptions(me.tenantId, { page: 1, pageSize: 20 }),
			)
			.catch(() => null);
		return { tenantId: me.tenantId };
	},
	component: ShopeeOrdersRoute,
});

function ShopeeOrdersRoute() {
	const { tenantId } = Route.useLoaderData();
	return <ShopeeOrdersPage tenantId={tenantId} />;
}
```

Add the nav entry (see Files list above for how to locate the nav component).

- [ ] **Step 6: Verify**

```bash
pnpm typecheck && pnpm test && pnpm check && pnpm extract
```

Expected: clean; `src/locales/en.json` gains the new `partner.shopeeOrders.*` keys.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(shopee-orders): orders page with status tabs, table, and polling"
```

---

### Task 14: Frontend — arrange-shipment modal, linking flow, wiring

**Files:**
- Create: `partner/src/features/partner/shopee-orders/components/arrange-shipment-modal.tsx`
- Create: `partner/src/features/partner/shopee-orders/components/needs-linking-actions.tsx`
- Modify: `partner/src/features/partner/shopee-orders/index.tsx` + `shopee-orders-table.tsx` (wire modal + linking actions)
- Test: `partner/src/features/partner/shopee-orders/components/arrange-shipment-modal.test.tsx`

- [ ] **Step 1: Write the failing modal test**

```tsx
import { MantineProvider } from "@mantine/core";
import { render, screen, waitFor } from "@testing-library/react";
import { IntlProvider } from "react-intl";
import { describe, expect, it, vi } from "vitest";
import { ArrangeShipmentModal } from "@/features/partner/shopee-orders/components/arrange-shipment-modal";

vi.mock("@/features/partner/shopee-orders/service", () => ({
	getShippingParameter: vi.fn().mockResolvedValue({
		supportsPickup: true,
		supportsDropoff: false,
		pickupAddresses: [
			{
				addressId: 1,
				address: "1 Jalan Test, KL",
				timeSlots: [
					{ pickupTimeId: "slot-1", date: "2026-07-17T00:00:00Z", timeText: "9am-12pm" },
				],
			},
		],
		dropoffBranches: [],
	}),
	shipShopeeOrder: vi.fn(),
}));

describe("ArrangeShipmentModal", () => {
	it("renders the pickup options returned by Shopee", async () => {
		render(
			<IntlProvider locale="en" onError={() => {}}>
				<MantineProvider>
					<ArrangeShipmentModal
						tenantId="tenant-a"
						orderId="order-1"
						orderSn="SN1"
						opened
						onClose={() => {}}
						onShipped={() => {}}
					/>
				</MantineProvider>
			</IntlProvider>,
		);
		await waitFor(() => {
			expect(screen.getByText("1 Jalan Test, KL")).toBeInTheDocument();
		});
	});
});
```

Note: the modal fetches with `useQuery`, so wrap the render in a `QueryClientProvider` with a fresh `QueryClient` — check how existing component tests set this up (`grep -rln "QueryClientProvider" partner/src --include="*.test.tsx"`) and mirror; if none exist, add the provider wrapper directly in this test.

- [ ] **Step 2: Run to verify failure** — `pnpm test -- arrange-shipment-modal` → FAIL.

- [ ] **Step 3: Implement the modal**

`arrange-shipment-modal.tsx` — props:

```tsx
interface ArrangeShipmentModalProps {
	tenantId: string;
	orderId: string;
	orderSn: string;
	opened: boolean;
	onClose: () => void;
	onShipped: () => void;
}
```

Behavior:
- On open, `useQuery({ queryKey: ["shopee-shipping-parameter", tenantId, orderId], queryFn: () => getShippingParameter(tenantId, orderId), enabled: opened })`; show `Loader` while pending; `<Alert role="alert" color="red">` on error.
- A `SegmentedControl` between "Pickup" and "Drop-off", only offering what `supportsPickup`/`supportsDropoff` allow (preselect the only supported one; hide the control when only one is supported).
- Pickup: `Select` of `pickupAddresses` (label = address), then `Select` of that address's `timeSlots` (label = `FormattedDate` of `date` + `timeText`). Drop-off: when `dropoffBranches` is non-empty a `Select` of branches, else a short confirmation `Text`.
- Confirm `Button` (disabled until the required choices are made) → `useMutation({ mutationFn: () => shipShopeeOrder(tenantId, orderId, payload) })` → on success `onShipped()` + `onClose()`; on error show the `<Alert>` with `partner.shopeeOrders.arrange.error` copy.
- Title: `FormattedMessage id="partner.shopeeOrders.arrange.title"` "Arrange shipment for {orderSn}". This is a short confirmation flow, not a create/edit form, so `Modal` (not Drawer) is correct per the conventions.

- [ ] **Step 4: Run the modal test** — PASS.

- [ ] **Step 5: Implement `needs-linking-actions.tsx` and wire everything**

`needs-linking-actions.tsx` — props `{ tenantId: string; order: ShopeeOrder; onOrderChanged: () => void }`. Renders:
- "Link product" `Button size="sm"` opening the shared `LinkProductDrawer` (from `@/components/shopee-linking/link-product-drawer`), pre-filtered/labeled with the first unresolved item's name/SKU; a secondary "Create & link" button opening `CreateLinkedProductDrawer`. Pass through whatever props those drawers require (open their files and match the prop contracts exactly — they were moved unchanged in Task 12, so their props are the same ones `shopee-products` passes).
- On drawer success: call `relinkShopeeOrder(tenantId, order.id)` then `onOrderChanged()`.
- A standalone "Re-check" `ActionIcon` (icon `RefreshCw`, i18n `aria-label`) that calls `relinkShopeeOrder` directly — for links created on the shopee-products page.

Wire in `index.tsx`: hold `orderBeingShipped: ShopeeOrder | null` state; `onArrangeShipment` sets it; render `<ArrangeShipmentModal opened={orderBeingShipped !== null} ... onShipped={() => queryClient.invalidateQueries({ queryKey: ["shopee-orders", tenantId] })} />`. `onOrderChanged` also invalidates that key.

- [ ] **Step 6: Full verification**

```bash
pnpm typecheck && pnpm test && pnpm check && pnpm extract
```

Expected: all clean. Manually smoke-test against the local backend (`pnpm dev`, backend + ngrok tunnel up): orders list renders, tabs filter, arrange modal shows courier options from a Shopee test shop, label downloads.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(shopee-orders): arrange-shipment modal, product-linking flow, label download"
```

---

## Final verification (both repos)

- [ ] `cd api && make build && make test` — clean build (warnings are errors), all tests green.
- [ ] `cd partner && pnpm typecheck && pnpm test && pnpm check` — clean.
- [ ] Manual E2E with a Shopee test shop through the ngrok tunnel (`ngrok.yml` at workspace root): register the push URL in the Shopee console, place a test order, watch it appear → link product if needed → arrange shipment → verify tracking number lands, waybill exists (`GET api/v1/waybills`), label downloads, and a Shopee-side cancellation cancels the Draft waybill. **Confirm push codes 3/4 match the console's order-status and tracking-number push channels; adjust the two constants in `ShopeeWebhookEventProcessor` if they differ.**
- [ ] Config: `Shopee:PushCallbackUrl`, `Shopee:OrderSync:Enabled=true` set in the dev environment.

## Out of scope (per spec)

Tenant-default shipping / auto-arrange scheduler, bulk arrange, packing-list printing, `IN_CANCEL` accept/reject, returns/refunds, multi-shop per tenant, token encryption (pre-existing gap).

