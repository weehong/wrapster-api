# Fulfillment Delegation Phase 2 (Auto-Match + Auto-Arrange) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Executor note (Codex or any agent without those skills):** execute tasks strictly in order, follow every step including the "run the test and watch it fail" steps, and commit after each task exactly as written. Never batch tasks. If a step's expected output does not match reality, STOP and re-read the surrounding source files instead of improvising.

**Goal:** Orders of tenants with an Active fulfillment delegation flow to a waybill with no human action: order items auto-link by SKU→barcode at ingestion, and a background job auto-arranges shipment.

**Architecture:** Three units: (1) a SKU→barcode auto-match extension inside `ShopeeOrderIngestionService.ResolveItemsAsync`, (2) `ShopeeOrderArrangeService` extracted from `ShipShopeeOrderCommandHandler` so a background job can arrange with an explicit `arrangedBy` actor, (3) `ShopeeAutoArrangeJob` (PeriodicTimer, Infrastructure) driving `ShopeeAutoArrangeProcessor` (Application) which sweeps Active-delegation tenants each cycle.

**Tech Stack:** .NET 10, Clean Architecture + CQRS (MediatR), EF Core/PostgreSQL, xUnit + Moq + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-07-24-fulfillment-delegation-phase2-design.md` (read it first).

## Global Constraints

- Working directory for all commands: the `api` repo root (branch `shopee`). Working tree must be clean before Task 1.
- **Never use `var`** — explicit types always (pre-commit hook rejects `var`).
- One type per file, no exceptions. No consecutive blank lines. `TreatWarningsAsErrors` is on.
- Domain mutations return `Result`/`Result<T>`; error constants live in the feature's Errors class, never inline `new Error(...)`.
- Every repository read/write is tenant-scoped except the explicitly documented cross-tenant job methods.
- Test baseline: 15 pre-existing failures in `Wrapsfer.Application.Tests` (NRE in `ShopeeConnectionTokenRefresher.RefreshIfNeededAsync`, hit by GetShopeeOrderLabel/ShipShopeeOrder/reconciliation/webhook tests). These are NOT regressions; do not fix them, do not count them as your failures. Every task's "expected" test results are stated relative to this baseline.
- Run `make format-all` before every commit.
- Commit messages: conventional commits (`feat(scope): ...`), ending with the trailer line `Co-Authored-By: Codex <noreply@openai.com>`.

---

### Task 1: SKU→barcode auto-match at ingestion

**Files:**
- Modify: `src/Wrapsfer.Application/Shopee/Services/ShopeeOrderIngestionService.cs`
- Test: `tests/Wrapsfer.Application.Tests/Shopee/ShopeeOrderIngestionServiceTests.cs`

**Interfaces:**
- Consumes: `IFulfillmentDelegationRepository.GetByTenantIdAsync(string, CancellationToken)`, `IProductRepository.GetByBarcodesAsync(IEnumerable<string>, string, CancellationToken)`, `ShopeeProductLink.Create(tenantId, productId, shopeeItemId, shopeeModelId, shopeeItemName, shopeeModelName, shopeeItemSku, linkedBy)`, `FulfillmentDelegationStatus.Active`.
- Produces: `ShopeeOrderIngestionService` constructor gains two parameters (order matters for tests): `IFulfillmentDelegationRepository delegationRepository` and `IProductRepository productRepository`, inserted after `IShopeeProductLinkRepository linkRepository`. Auto-created links use `LinkedBy == "system:auto-match"`.

- [ ] **Step 1: Write the failing tests**

Add to `tests/Wrapsfer.Application.Tests/Shopee/ShopeeOrderIngestionServiceTests.cs`. The class already has `_productRepository`; add a delegation repo mock field next to the other mocks, update the service construction in the constructor, and add a detail factory with a SKU. The constructor currently builds the service as:

```csharp
_service = new ShopeeOrderIngestionService(
    _gateway.Object,
    _orderRepository.Object,
    _linkRepository.Object,
    cancellationService,
    _unitOfWork.Object,
    NullLogger<ShopeeOrderIngestionService>.Instance);
```

Change it to (and add the new mock field + default setup):

```csharp
private readonly Mock<IFulfillmentDelegationRepository> _delegationRepository = new();
```

```csharp
_service = new ShopeeOrderIngestionService(
    _gateway.Object,
    _orderRepository.Object,
    _linkRepository.Object,
    _delegationRepository.Object,
    _productRepository.Object,
    cancellationService,
    _unitOfWork.Object,
    NullLogger<ShopeeOrderIngestionService>.Instance);

_delegationRepository
    .Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
    .ReturnsAsync((FulfillmentDelegation?)null);
```

Add these helpers and tests at the bottom of the class:

```csharp
private static FulfillmentDelegation CreateActiveDelegation()
{
    FulfillmentDelegation delegation =
        FulfillmentDelegation.Request(TenantId, FulfillmentShippingMethod.Dropoff).Value;
    delegation.Accept();
    return delegation;
}

private static ShopeeOrderDetail CreateDetailWithSku(string? sku) => new(
    "SN1", "READY_TO_SHIP", "MY", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", null,
    [new ShopeeOrderDetailItem(111, 0, "Item", null, sku, 2)]);

private void SetupUnknownOrder(string? sku)
{
    _orderRepository
        .Setup(r => r.GetByOrderSnAsync("SN1", TenantId, It.IsAny<CancellationToken>()))
        .ReturnsAsync((ShopeeOrder?)null);
    _gateway
        .Setup(g => g.GetOrderDetailAsync(1001, "access-token", "SN1", It.IsAny<CancellationToken>()))
        .ReturnsAsync(Result<ShopeeOrderDetail>.Success(CreateDetailWithSku(sku)));
}

[Fact]
public async Task IngestOrderAsync_NoLink_ActiveDelegation_SkuMatchesBarcode_ResolvesAndPersistsLink()
{
    Product product = ProductTestFactory.CreateSingle(TenantId, barcode: "BC-1");
    SetupUnknownOrder("BC-1");
    _delegationRepository
        .Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(CreateActiveDelegation());
    _productRepository
        .Setup(r => r.GetByBarcodesAsync(
            It.Is<IEnumerable<string>>(b => b.Single() == "BC-1"),
            TenantId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<Product> { product });
    ShopeeOrder? added = null;
    _orderRepository.Setup(r => r.Add(It.IsAny<ShopeeOrder>()))
        .Callback<ShopeeOrder>(o => added = o);

    Result result = await _service.IngestOrderAsync(CreateConnection(), "SN1", CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    added.Should().NotBeNull();
    added!.Status.Should().Be(ShopeeOrderStatus.ReadyToShip);
    added.Items.Single().ProductId.Should().Be(product.Id);
    _linkRepository.Verify(r => r.Add(It.Is<ShopeeProductLink>(l =>
        l.TenantId == TenantId
        && l.ProductId == product.Id
        && l.ShopeeItemId == 111
        && l.ShopeeModelId == 0
        && l.LinkedBy == "system:auto-match")), Times.Once);
}

[Fact]
public async Task IngestOrderAsync_NoLink_NoDelegation_DoesNotAutoMatch()
{
    SetupUnknownOrder("BC-1");
    ShopeeOrder? added = null;
    _orderRepository.Setup(r => r.Add(It.IsAny<ShopeeOrder>()))
        .Callback<ShopeeOrder>(o => added = o);

    Result result = await _service.IngestOrderAsync(CreateConnection(), "SN1", CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    added!.Status.Should().Be(ShopeeOrderStatus.NeedsLinking);
    _productRepository.Verify(r => r.GetByBarcodesAsync(
        It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    _linkRepository.Verify(r => r.Add(It.IsAny<ShopeeProductLink>()), Times.Never);
}

[Fact]
public async Task IngestOrderAsync_NoLink_RequestedDelegation_DoesNotAutoMatch()
{
    SetupUnknownOrder("BC-1");
    _delegationRepository
        .Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(FulfillmentDelegation.Request(TenantId, FulfillmentShippingMethod.Dropoff).Value);
    ShopeeOrder? added = null;
    _orderRepository.Setup(r => r.Add(It.IsAny<ShopeeOrder>()))
        .Callback<ShopeeOrder>(o => added = o);

    Result result = await _service.IngestOrderAsync(CreateConnection(), "SN1", CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    added!.Status.Should().Be(ShopeeOrderStatus.NeedsLinking);
    _productRepository.Verify(r => r.GetByBarcodesAsync(
        It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
}

[Fact]
public async Task IngestOrderAsync_NoLink_ActiveDelegation_BlankSku_DoesNotLookupProducts()
{
    SetupUnknownOrder("  ");
    _delegationRepository
        .Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(CreateActiveDelegation());
    ShopeeOrder? added = null;
    _orderRepository.Setup(r => r.Add(It.IsAny<ShopeeOrder>()))
        .Callback<ShopeeOrder>(o => added = o);

    Result result = await _service.IngestOrderAsync(CreateConnection(), "SN1", CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    added!.Status.Should().Be(ShopeeOrderStatus.NeedsLinking);
    _productRepository.Verify(r => r.GetByBarcodesAsync(
        It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
}

[Fact]
public async Task IngestOrderAsync_NoLink_ActiveDelegation_BarcodeMiss_StaysNeedsLinking()
{
    SetupUnknownOrder("BC-UNKNOWN");
    _delegationRepository
        .Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(CreateActiveDelegation());
    _productRepository
        .Setup(r => r.GetByBarcodesAsync(
            It.IsAny<IEnumerable<string>>(), TenantId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<Product>());
    ShopeeOrder? added = null;
    _orderRepository.Setup(r => r.Add(It.IsAny<ShopeeOrder>()))
        .Callback<ShopeeOrder>(o => added = o);

    Result result = await _service.IngestOrderAsync(CreateConnection(), "SN1", CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    added!.Status.Should().Be(ShopeeOrderStatus.NeedsLinking);
    _linkRepository.Verify(r => r.Add(It.IsAny<ShopeeProductLink>()), Times.Never);
}
```

Note: `CreateDetail(...)` in the file shows the exact `ShopeeOrderDetail` positional shape — if `CreateDetailWithSku` fails to compile, mirror `CreateDetail` and only change the `ShopeeOrderDetailItem` SKU argument. `ProductTestFactory` is in `tests/Wrapsfer.Application.Tests/Helpers/`.

- [ ] **Step 2: Run the new tests to verify they fail**

Run: `dotnet build 2>&1 | tail -n 5`
Expected: build FAILS — `ShopeeOrderIngestionService` has no constructor taking 8 arguments. (A compile failure is this step's red state.)

- [ ] **Step 3: Implement auto-match in the ingestion service**

In `src/Wrapsfer.Application/Shopee/Services/ShopeeOrderIngestionService.cs`, change the constructor parameter list to:

```csharp
public sealed class ShopeeOrderIngestionService(
    IShopeeGateway shopeeGateway,
    IShopeeOrderRepository orderRepository,
    IShopeeProductLinkRepository linkRepository,
    IFulfillmentDelegationRepository delegationRepository,
    IProductRepository productRepository,
    ShopeeOrderCancellationService cancellationService,
    IUnitOfWork unitOfWork,
    ILogger<ShopeeOrderIngestionService> logger)
```

Add a constant next to the existing ones:

```csharp
private const string AutoMatchActor = "system:auto-match";
```

Replace `ResolveItemsAsync` with:

```csharp
private async Task<IReadOnlyList<ShopeeOrderItemSnapshot>> ResolveItemsAsync(
    string tenantId,
    IReadOnlyList<ShopeeOrderDetailItem> items,
    CancellationToken cancellationToken)
{
    IReadOnlyList<ShopeeProductLink> links =
        await linkRepository.ListByTenantAsync(tenantId, cancellationToken);
    Dictionary<(long ItemId, long ModelId), Guid> productByUnit = links.ToDictionary(
        l => (l.ShopeeItemId, l.ShopeeModelId), l => l.ProductId);

    List<ShopeeOrderDetailItem> unresolved = items
        .Where(i => !productByUnit.ContainsKey((i.ItemId, i.ModelId)))
        .ToList();
    if (unresolved.Count > 0)
    {
        Dictionary<(long ItemId, long ModelId), Guid> autoMatched =
            await AutoMatchBySkuAsync(tenantId, unresolved, cancellationToken);
        foreach (KeyValuePair<(long ItemId, long ModelId), Guid> match in autoMatched)
        {
            productByUnit[match.Key] = match.Value;
        }
    }

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

/// <summary>
/// For tenants with an Active fulfillment delegation, unlinked order lines whose Shopee
/// SKU exactly matches a product barcode (ordinal, trimmed) are linked automatically.
/// The created ShopeeProductLink persists with the order in the caller's SaveChanges,
/// so future orders for the same item resolve from the link table directly.
/// </summary>
private async Task<Dictionary<(long ItemId, long ModelId), Guid>> AutoMatchBySkuAsync(
    string tenantId,
    IReadOnlyList<ShopeeOrderDetailItem> unresolved,
    CancellationToken cancellationToken)
{
    Dictionary<(long ItemId, long ModelId), Guid> matches = [];
    FulfillmentDelegation? delegation =
        await delegationRepository.GetByTenantIdAsync(tenantId, cancellationToken);
    if (delegation is null || delegation.Status != FulfillmentDelegationStatus.Active)
    {
        return matches;
    }

    List<string> skus = unresolved
        .Select(i => i.ItemSku?.Trim())
        .Where(s => !string.IsNullOrEmpty(s))
        .Cast<string>()
        .Distinct(StringComparer.Ordinal)
        .ToList();
    if (skus.Count == 0)
    {
        return matches;
    }

    IReadOnlyList<Product> products =
        await productRepository.GetByBarcodesAsync(skus, tenantId, cancellationToken);
    Dictionary<string, Guid> productByBarcode = products.ToDictionary(
        p => p.Barcode, p => p.Id, StringComparer.Ordinal);

    foreach (ShopeeOrderDetailItem item in unresolved)
    {
        string? sku = item.ItemSku?.Trim();
        if (string.IsNullOrEmpty(sku)
            || matches.ContainsKey((item.ItemId, item.ModelId))
            || !productByBarcode.TryGetValue(sku, out Guid productId))
        {
            continue;
        }

        Result<ShopeeProductLink> linkResult = ShopeeProductLink.Create(
            tenantId, productId, item.ItemId, item.ModelId,
            item.ItemName, item.ModelName, item.ItemSku, AutoMatchActor);
        if (linkResult.IsFailure)
        {
            logger.LogWarning(
                "Auto-match link creation failed for tenant {TenantId} item {ItemId}/{ModelId}: {ErrorCode}",
                tenantId, item.ItemId, item.ModelId, linkResult.Error.Code);
            continue;
        }

        linkRepository.Add(linkResult.Value);
        matches[(item.ItemId, item.ModelId)] = productId;
    }

    return matches;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build 2>&1 | tail -n 3 && dotnet test --no-restore --no-build --filter "FullyQualifiedName~ShopeeOrderIngestionServiceTests" 2>&1 | tail -n 5`
Expected: build succeeds; all `ShopeeOrderIngestionServiceTests` PASS (pre-existing tests plus the 5 new ones).

- [ ] **Step 5: Check for other ShopeeOrderIngestionService constructions**

Run: `grep -rn "new ShopeeOrderIngestionService(" src/ tests/ --include="*.cs" | grep -v obj`
Expected: only the test file constructs it directly (DI handles production). If another construction site appears, add the two new mock/instance arguments there in the same positions.

- [ ] **Step 6: Run the full Application test suite**

Run: `dotnet test --no-restore --no-build tests/Wrapsfer.Application.Tests 2>&1 | tail -n 3`
Expected: `Failed: 15` (the known `ShopeeConnectionTokenRefresher` baseline), everything else passes. If Failed > 15, diff the failing test names against the baseline (GetShopeeOrderLabel/ShipShopeeOrder/reconciliation/webhook token-refresher tests) and fix your regression.

- [ ] **Step 7: Format and commit**

```bash
make format-all
git add -A
git commit -m "feat(shopee): auto-match order items by SKU to barcode for delegated tenants

For tenants with an Active fulfillment delegation, ingestion now links
unresolved order lines whose Shopee SKU exactly matches a product
barcode, persisting a ShopeeProductLink (LinkedBy=system:auto-match) so
future orders resolve directly.

Co-Authored-By: Codex <noreply@openai.com>"
```

---

### Task 2: Shipping-parameter selector (pure logic)

**Files:**
- Modify: `src/Wrapsfer.Domain/Errors/ShopeeOrderErrors.cs` (add one error)
- Create: `src/Wrapsfer.Application/Shopee/Services/ShopeeAutoArrangeParamSelector.cs`
- Test: `tests/Wrapsfer.Application.Tests/Shopee/ShopeeAutoArrangeParamSelectorTests.cs`

**Interfaces:**
- Consumes: `ShopeeShippingParameter(bool SupportsPickup, bool SupportsDropoff, IReadOnlyList<ShopeePickupAddress> PickupAddresses, IReadOnlyList<ShopeeDropoffBranch> DropoffBranches)`; `ShopeePickupAddress(long AddressId, string Address, IReadOnlyList<ShopeePickupTimeSlot> TimeSlots)`; `ShopeePickupTimeSlot(string PickupTimeId, DateTime Date, string? TimeText)`; `ShopeeDropoffBranch(long BranchId, string Address)`; `ShopeeShipOrderRequest(string OrderSn, ShopeeShipOrderPickup? Pickup, ShopeeShipOrderDropoff? Dropoff)`; `ShopeeShipOrderPickup(long AddressId, string PickupTimeId)`; `ShopeeShipOrderDropoff(long? BranchId)`; `FulfillmentShippingMethod { Dropoff, Pickup }`.
- Produces: `static Result<ShopeeShipOrderRequest> ShopeeAutoArrangeParamSelector.Select(string orderSn, FulfillmentShippingMethod preferredMethod, ShopeeShippingParameter parameter)` and `ShopeeOrderErrors.NoUsableShippingOption` (code `ShopeeOrder.NoUsableShippingOption`, `ErrorType.Failure`). Task 4 depends on both.

- [ ] **Step 1: Write the failing tests**

Create `tests/Wrapsfer.Application.Tests/Shopee/ShopeeAutoArrangeParamSelectorTests.cs`:

```csharp
using FluentAssertions;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Application.Tests.Shopee;

public sealed class ShopeeAutoArrangeParamSelectorTests
{
    private const string OrderSn = "SN1";

    private static ShopeePickupAddress Address(long id, params ShopeePickupTimeSlot[] slots) =>
        new(id, $"address-{id}", slots);

    private static ShopeePickupTimeSlot Slot(string id, int day) =>
        new(id, new DateTime(2026, 7, day, 0, 0, 0, DateTimeKind.Utc), null);

    [Fact]
    public void Select_PreferDropoff_DropoffSupported_UsesFirstBranch()
    {
        ShopeeShippingParameter parameter = new(
            SupportsPickup: true, SupportsDropoff: true,
            [Address(1, Slot("t1", 25))],
            [new ShopeeDropoffBranch(77, "branch"), new ShopeeDropoffBranch(88, "other")]);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Dropoff, parameter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Pickup.Should().BeNull();
        result.Value.Dropoff!.BranchId.Should().Be(77);
    }

    [Fact]
    public void Select_PreferDropoff_NoBranchesListed_UsesBranchlessDropoff()
    {
        ShopeeShippingParameter parameter = new(false, true, [], []);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Dropoff, parameter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dropoff!.BranchId.Should().BeNull();
    }

    [Fact]
    public void Select_PreferPickup_PickupSupported_UsesEarliestSlot()
    {
        ShopeeShippingParameter parameter = new(
            true, true, [Address(5, Slot("late", 28), Slot("early", 25))], []);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Pickup, parameter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dropoff.Should().BeNull();
        result.Value.Pickup!.AddressId.Should().Be(5);
        result.Value.Pickup.PickupTimeId.Should().Be("early");
    }

    [Fact]
    public void Select_PreferPickup_FirstAddressHasNoSlots_UsesNextAddress()
    {
        ShopeeShippingParameter parameter = new(
            true, false, [Address(1), Address(2, Slot("t2", 26))], []);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Pickup, parameter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Pickup!.AddressId.Should().Be(2);
    }

    [Fact]
    public void Select_PreferPickup_PickupUnsupported_FallsBackToDropoff()
    {
        ShopeeShippingParameter parameter = new(
            false, true, [], [new ShopeeDropoffBranch(9, "branch")]);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Pickup, parameter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dropoff!.BranchId.Should().Be(9);
    }

    [Fact]
    public void Select_PreferPickup_PickupSupportedButNoSlots_FallsBackToDropoff()
    {
        ShopeeShippingParameter parameter = new(true, true, [Address(1)], []);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Pickup, parameter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Dropoff.Should().NotBeNull();
    }

    [Fact]
    public void Select_PreferDropoff_DropoffUnsupported_FallsBackToPickup()
    {
        ShopeeShippingParameter parameter = new(
            true, false, [Address(3, Slot("t3", 27))], []);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Dropoff, parameter);

        result.IsSuccess.Should().BeTrue();
        result.Value.Pickup!.AddressId.Should().Be(3);
    }

    [Fact]
    public void Select_NeitherMethodSupported_ReturnsNoUsableShippingOption()
    {
        ShopeeShippingParameter parameter = new(false, false, [], []);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Dropoff, parameter);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.NoUsableShippingOption);
    }

    [Fact]
    public void Select_PickupOnlyWithNoSlotsAnywhere_ReturnsNoUsableShippingOption()
    {
        ShopeeShippingParameter parameter = new(true, false, [Address(1), Address(2)], []);

        Result<ShopeeShipOrderRequest> result = ShopeeAutoArrangeParamSelector.Select(
            OrderSn, FulfillmentShippingMethod.Pickup, parameter);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.NoUsableShippingOption);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet build 2>&1 | tail -n 5`
Expected: build FAILS — `ShopeeAutoArrangeParamSelector` and `NoUsableShippingOption` do not exist.

- [ ] **Step 3: Implement the error and the selector**

Append to `src/Wrapsfer.Domain/Errors/ShopeeOrderErrors.cs` (inside the class, following the existing style):

```csharp
public static readonly Error NoUsableShippingOption = new(
    "ShopeeOrder.NoUsableShippingOption",
    "Neither dropoff nor pickup offers a usable shipping option for this order",
    ErrorType.Failure);
```

Create `src/Wrapsfer.Application/Shopee/Services/ShopeeAutoArrangeParamSelector.cs`:

```csharp
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Application.Shopee.Services;

/// <summary>
/// Picks concrete ship_order parameters for auto-arrange: preferred method first, the
/// other method as fallback. Dropoff uses the first branch (branchless when Shopee lists
/// none); pickup uses the first address exposing a time slot, taking its earliest slot.
/// No concrete option from either method is a permanent failure the caller records.
/// </summary>
public static class ShopeeAutoArrangeParamSelector
{
    public static Result<ShopeeShipOrderRequest> Select(
        string orderSn, FulfillmentShippingMethod preferredMethod, ShopeeShippingParameter parameter)
    {
        FulfillmentShippingMethod[] methodsInOrder = preferredMethod == FulfillmentShippingMethod.Pickup
            ? [FulfillmentShippingMethod.Pickup, FulfillmentShippingMethod.Dropoff]
            : [FulfillmentShippingMethod.Dropoff, FulfillmentShippingMethod.Pickup];

        foreach (FulfillmentShippingMethod method in methodsInOrder)
        {
            ShopeeShipOrderRequest? request = method == FulfillmentShippingMethod.Dropoff
                ? TryDropoff(orderSn, parameter)
                : TryPickup(orderSn, parameter);
            if (request is not null)
            {
                return Result<ShopeeShipOrderRequest>.Success(request);
            }
        }

        return Result<ShopeeShipOrderRequest>.Failure(ShopeeOrderErrors.NoUsableShippingOption);
    }

    private static ShopeeShipOrderRequest? TryDropoff(string orderSn, ShopeeShippingParameter parameter)
    {
        if (!parameter.SupportsDropoff)
        {
            return null;
        }

        long? branchId = parameter.DropoffBranches.Count > 0
            ? parameter.DropoffBranches[0].BranchId
            : null;
        return new ShopeeShipOrderRequest(orderSn, null, new ShopeeShipOrderDropoff(branchId));
    }

    private static ShopeeShipOrderRequest? TryPickup(string orderSn, ShopeeShippingParameter parameter)
    {
        if (!parameter.SupportsPickup)
        {
            return null;
        }

        foreach (ShopeePickupAddress address in parameter.PickupAddresses)
        {
            ShopeePickupTimeSlot? slot = address.TimeSlots.OrderBy(s => s.Date).FirstOrDefault();
            if (slot is null)
            {
                continue;
            }

            return new ShopeeShipOrderRequest(
                orderSn, new ShopeeShipOrderPickup(address.AddressId, slot.PickupTimeId), null);
        }

        return null;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build 2>&1 | tail -n 3 && dotnet test --no-restore --no-build --filter "FullyQualifiedName~ShopeeAutoArrangeParamSelectorTests" 2>&1 | tail -n 5`
Expected: PASS, 9/9.

- [ ] **Step 5: Format and commit**

```bash
make format-all
git add -A
git commit -m "feat(shopee): add auto-arrange shipping parameter selector

Prefer-then-fallback selection of concrete ship_order params: dropoff
takes the first branch (branchless when none listed), pickup takes the
first address with a slot at its earliest date. No concrete option from
either method returns ShopeeOrder.NoUsableShippingOption.

Co-Authored-By: Codex <noreply@openai.com>"
```

---

### Task 3: Extract ShopeeOrderArrangeService from the ship handler

**Files:**
- Create: `src/Wrapsfer.Application/Shopee/Services/ShopeeOrderArrangeService.cs`
- Modify: `src/Wrapsfer.Application/Shopee/Commands/ShipShopeeOrder/ShipShopeeOrderCommandHandler.cs`
- Modify: `src/Wrapsfer.Application/DependencyInjection.cs` (register the service)
- Test: `tests/Wrapsfer.Application.Tests/Shopee/ShopeeOrderArrangeServiceTests.cs` (new) — and the existing `tests/Wrapsfer.Application.Tests/Shopee/ShipShopeeOrderCommandHandlerTests.cs` is the regression net: ONLY its constructor wiring may change (the handler's constructor shrinks); every `[Fact]` method, helper, and assertion stays byte-for-byte untouched. Its fixture currently builds the handler as `new ShipShopeeOrderCommandHandler(_orderRepository.Object, _connectionRepository.Object, _gateway.Object, _tokenRefresher, _completionService, _tenantContext.Object, _unitOfWork.Object)`; change that call to `new ShipShopeeOrderCommandHandler(_orderRepository.Object, _connectionRepository.Object, _tokenRefresher, new ShopeeOrderArrangeService(_gateway.Object, _completionService, _unitOfWork.Object), _tenantContext.Object)` — the same mocks feed the real arrange service, so all existing behavioral assertions keep exercising the same flow.

**Interfaces:**
- Consumes: `ShopeeOrderShipmentCompletionService.CompleteAsync(ShopeeOrder, string, CancellationToken)`, `IShopeeGateway.ShipOrderAsync` / `GetTrackingNumberAsync`, `ShopeeOrder.MarkShipmentArranged(string? arrangedBy, DateTime arrangedAt)` / `MarkShipmentFailed(string, DateTime)`.
- Produces: `public sealed class ShopeeOrderArrangeService` (scoped DI) with method `Task<Result> ArrangeAsync(ShopeeOrder order, ShopeeShopConnection connection, ShopeeShipOrderRequest shipRequest, string? arrangedBy, CancellationToken cancellationToken)`. Task 4 calls this with `arrangedBy: "system:auto-arrange"`. Behavior contract: identical to today's handler flow (guards, resume path, failure marking, opportunistic completion).

- [ ] **Step 1: Read the current handler completely**

Read `src/Wrapsfer.Application/Shopee/Commands/ShipShopeeOrder/ShipShopeeOrderCommandHandler.cs` top to bottom before touching anything. The extraction below moves its logic verbatim; the comments move with the code.

- [ ] **Step 2: Write a failing test for the extracted service**

Create `tests/Wrapsfer.Application.Tests/Shopee/ShopeeOrderArrangeServiceTests.cs`. Borrow the order/connection factory idioms from `ShipShopeeOrderCommandHandlerTests.cs` (same file layout, same mocks). The test must cover what the handler tests cannot: an explicit non-HTTP actor.

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee;

public sealed class ShopeeOrderArrangeServiceTests
{
    private const string TenantId = "tenant-a";
    private static readonly DateTime Now = new(2026, 7, 24, 8, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IShopeeGateway> _gateway = new();
    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IProductComponentRepository> _productComponentRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ShopeeOrderArrangeService _service;

    public ShopeeOrderArrangeServiceTests()
    {
        ShopeeOrderShipmentCompletionService completionService = new(
            _waybillRepository.Object,
            _productRepository.Object,
            new StockReservationService(_productRepository.Object, _productComponentRepository.Object),
            _unitOfWork.Object,
            NullLogger<ShopeeOrderShipmentCompletionService>.Instance);
        _service = new ShopeeOrderArrangeService(_gateway.Object, completionService, _unitOfWork.Object);
    }

    private static ShopeeShopConnection CreateConnection() => ShopeeShopConnection.Create(
        TenantId, 1001, "access-token", "refresh-token",
        Now.AddHours(1), Now.AddDays(30), Now, "linker").Value;

    private static ShopeeOrder CreateReadyToShipOrder()
    {
        ShopeeOrderSnapshot snapshot = new(
            "READY_TO_SHIP", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", null);
        return ShopeeOrder.Create(TenantId, "SN1", "MY", snapshot,
            [new ShopeeOrderItemSnapshot(111, 0, "Item", null, null, 2, Guid.NewGuid())], Now).Value;
    }

    [Fact]
    public async Task ArrangeAsync_Success_RecordsExplicitActor()
    {
        ShopeeOrder order = CreateReadyToShipOrder();
        _gateway
            .Setup(g => g.ShipOrderAsync(1001, "access-token",
                It.IsAny<ShopeeShipOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _gateway
            .Setup(g => g.GetTrackingNumberAsync(1001, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string?>.Success(null));

        Result result = await _service.ArrangeAsync(
            order, CreateConnection(),
            new ShopeeShipOrderRequest("SN1", null, new ShopeeShipOrderDropoff(null)),
            "system:auto-arrange", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.AwaitingTracking);
        order.ShipmentArrangedBy.Should().Be("system:auto-arrange");
    }

    [Fact]
    public async Task ArrangeAsync_ShipOrderFails_MarksShipmentFailed()
    {
        ShopeeOrder order = CreateReadyToShipOrder();
        _gateway
            .Setup(g => g.ShipOrderAsync(1001, "access-token",
                It.IsAny<ShopeeShipOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(new Error("Shopee.ShipRejected", "rejected", ErrorType.Failure)));

        Result result = await _service.ArrangeAsync(
            order, CreateConnection(),
            new ShopeeShipOrderRequest("SN1", null, new ShopeeShipOrderDropoff(null)),
            "system:auto-arrange", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.ShipmentRequestFailed);
        order.Status.Should().Be(ShopeeOrderStatus.ShipmentFailed);
    }
}
```

Adjust the two factory helpers only if their positional argument lists do not compile — copy the working shapes from `ShipShopeeOrderCommandHandlerTests.cs` / `ShopeeOrderIngestionServiceTests.cs` in that case; property name `ShipmentArrangedBy` exists on `ShopeeOrder` (verify with `grep -n "ShipmentArrangedBy" src/Wrapsfer.Domain/Entities/ShopeeOrder.cs`; if the property has a different name, use that name in the assertion). `ShopeeOrderErrors.ShipmentRequestFailed` already exists (used by the current handler).

- [ ] **Step 3: Run to verify failure**

Run: `dotnet build 2>&1 | tail -n 5`
Expected: build FAILS — `ShopeeOrderArrangeService` does not exist.

- [ ] **Step 4: Create the service and slim the handler**

Create `src/Wrapsfer.Application/Shopee/Services/ShopeeOrderArrangeService.cs` — the bodies below are the current handler's logic moved verbatim (including comments), with `tenantContext.Username` replaced by the `arrangedBy` parameter and the connection/token handling left to callers:

```csharp
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Application.Shopee.Services;

/// <summary>
/// Arranges a Shopee shipment for an order: guards the local and Shopee status, calls
/// ship_order, records the arranging actor, and opportunistically completes with a
/// tracking number. Shared by the partner-facing ship command (actor = username) and the
/// auto-arrange job (actor = system). Callers load the connection and refresh its token.
/// </summary>
public sealed class ShopeeOrderArrangeService(
    IShopeeGateway shopeeGateway,
    ShopeeOrderShipmentCompletionService completionService,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> ArrangeAsync(
        ShopeeOrder order,
        ShopeeShopConnection connection,
        ShopeeShipOrderRequest shipRequest,
        string? arrangedBy,
        CancellationToken cancellationToken)
    {
        if (order.Status is not (ShopeeOrderStatus.ReadyToShip or ShopeeOrderStatus.ShipmentFailed))
        {
            return Result.Failure(ShopeeOrderErrors.NotReadyToShip);
        }

        if (order.HasUnresolvedItems)
        {
            return Result.Failure(ShopeeOrderErrors.ItemsNotLinked);
        }

        // Spec: IN_CANCEL (buyer requested cancellation, unresolved on Shopee) blocks
        // arranging new shipment but cancels nothing.
        if (string.Equals(order.ShopeeStatus, "IN_CANCEL", StringComparison.Ordinal))
        {
            return Result.Failure(ShopeeOrderErrors.CancellationRequested);
        }

        // ShipmentFailed with an arrangement timestamp means ship_order already succeeded
        // on Shopee's side and only local completion failed (tracking conflict, stock,
        // missing product). Re-running ship_order can never succeed here — Shopee would
        // reject the order as already arranged. Skip straight to the completion retry.
        if (order.Status == ShopeeOrderStatus.ShipmentFailed && order.ShipmentArrangedAt is not null)
        {
            return await ResumeAfterArrangedShipmentAsync(order, connection, arrangedBy, cancellationToken);
        }

        // The order's local ReadyToShip/ShipmentFailed status can be stale relative to
        // Shopee if the shipment was already arranged there (Seller Centre, another
        // integration, or a prior attempt whose local status update was lost). Re-running
        // ship_order against Shopee in that state only errors out on Shopee's side.
        if (order.ShopeeStatus is "PROCESSED" or "SHIPPED" or "COMPLETED" or "TO_CONFIRM_RECEIVE")
        {
            return Result.Failure(ShopeeOrderErrors.AlreadyArrangedOnShopee);
        }

        Result shipResult = await shopeeGateway.ShipOrderAsync(
            connection.ShopId, connection.AccessToken, shipRequest, cancellationToken);
        if (shipResult.IsFailure)
        {
            Result failResult = order.MarkShipmentFailed(shipResult.Error.Description, DateTime.UtcNow);
            if (failResult.IsSuccess)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result.Failure(ShopeeOrderErrors.ShipmentRequestFailed);
        }

        Result arrangeResult = order.MarkShipmentArranged(arrangedBy, DateTime.UtcNow);
        if (arrangeResult.IsFailure)
        {
            return Result.Failure(arrangeResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await TryCompleteWithTrackingAsync(order, connection, cancellationToken);

        return Result.Success();
    }

    private async Task<Result> ResumeAfterArrangedShipmentAsync(
        ShopeeOrder order, ShopeeShopConnection connection, string? arrangedBy,
        CancellationToken cancellationToken)
    {
        Result arrangeResult = order.MarkShipmentArranged(arrangedBy, DateTime.UtcNow);
        if (arrangeResult.IsFailure)
        {
            return Result.Failure(arrangeResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await TryCompleteWithTrackingAsync(order, connection, cancellationToken);

        return Result.Success();
    }

    // Opportunistic: Shopee often assigns the tracking number within seconds. A failure
    // here is not an error — the tracking push or reconciliation completes it later.
    private async Task TryCompleteWithTrackingAsync(
        ShopeeOrder order, ShopeeShopConnection connection, CancellationToken cancellationToken)
    {
        Result<string?> trackingResult = await shopeeGateway.GetTrackingNumberAsync(
            connection.ShopId, connection.AccessToken, order.OrderSn, cancellationToken);
        if (trackingResult.IsSuccess && !string.IsNullOrWhiteSpace(trackingResult.Value))
        {
            await completionService.CompleteAsync(order, trackingResult.Value, cancellationToken);
        }
    }
}
```

Rewrite `ShipShopeeOrderCommandHandler` to delegate. IMPORTANT: the handler keeps its early guard block (status / unresolved items / IN_CANCEL / resume detection) so error precedence with a missing connection is unchanged — existing tests assert, e.g., `NotReadyToShip` without ever mocking a connection:

```csharp
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Common;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Commands.ShipShopeeOrder;

internal sealed class ShipShopeeOrderCommandHandler(
    IShopeeOrderRepository orderRepository,
    IShopeeShopConnectionRepository connectionRepository,
    ShopeeConnectionTokenRefresher tokenRefresher,
    ShopeeOrderArrangeService arrangeService,
    ITenantContext tenantContext) : ICommandHandler<ShipShopeeOrderCommand, ShopeeOrderResponse>
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

        // Pre-checks duplicate the arrange service's guards on purpose: they preserve the
        // HTTP flow's error precedence (a bad order status must surface before a missing
        // connection, which is only loaded afterwards).
        if (order.Status is not (ShopeeOrderStatus.ReadyToShip or ShopeeOrderStatus.ShipmentFailed))
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.NotReadyToShip);
        }

        if (order.HasUnresolvedItems)
        {
            return Result<ShopeeOrderResponse>.Failure(ShopeeOrderErrors.ItemsNotLinked);
        }

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

        Result arrangeResult = await arrangeService.ArrangeAsync(
            order, connection, shipRequest, tenantContext.Username, cancellationToken);
        if (arrangeResult.IsFailure)
        {
            return Result<ShopeeOrderResponse>.Failure(arrangeResult.Error);
        }

        return Result<ShopeeOrderResponse>.Success(ShopeeOrderResponseMapper.Map(order));
    }
}
```

Behavior notes against the old handler, all preserved: the resume path previously loaded the connection AFTER detecting resume and tolerated a null connection (arranged + saved, skipped completion). The new handler loads the connection before calling the service, so a resume with a missing connection now returns `ConnectionNotFound` instead of half-succeeding — check `ShipShopeeOrderCommandHandlerTests` for a test pinning the old tolerant behavior. If such a test exists, STOP and restore the old semantics by keeping the resume branch in the handler (detect `order.Status == ShopeeOrderStatus.ShipmentFailed && order.ShipmentArrangedAt is not null` before the connection load, and in that branch load the connection, tolerate null, and call the service only when it exists). If no such test exists, the stricter order is acceptable — note it in the commit message body.

Register the service in `src/Wrapsfer.Application/DependencyInjection.cs`, after the `ShopeeOrderShipmentCompletionService` line:

```csharp
services.AddScoped<ShopeeOrderArrangeService>();
```

The old handler's `IUnitOfWork` and `IShopeeGateway` constructor parameters are gone (the service owns them). If the compiler flags unused usings in the handler, remove them.

- [ ] **Step 5: Run the new service tests and the existing handler tests**

Run: `dotnet build 2>&1 | tail -n 3 && dotnet test --no-restore --no-build --filter "FullyQualifiedName~ShopeeOrderArrangeServiceTests|FullyQualifiedName~ShipShopeeOrderCommandHandlerTests" 2>&1 | tail -n 8`
Expected: `ShopeeOrderArrangeServiceTests` PASS. `ShipShopeeOrderCommandHandlerTests` show exactly the same pass/fail split as before this task (some are in the 15-test NRE baseline — compare failing test NAMES against a pre-task run if unsure; run the filter once on a clean checkout stash if you did not record it).

- [ ] **Step 6: Full Application suite**

Run: `dotnet test --no-restore --no-build tests/Wrapsfer.Application.Tests 2>&1 | tail -n 3`
Expected: `Failed: 15` baseline, nothing new.

- [ ] **Step 7: Format and commit**

```bash
make format-all
git add -A
git commit -m "refactor(shopee): extract ShopeeOrderArrangeService with explicit actor

Moves the ship handler's arrange flow (guards, resume-after-arranged,
ship_order, opportunistic tracking completion) into a shared service
taking arrangedBy explicitly, so the auto-arrange job can act as
system:auto-arrange outside an HTTP tenant context.

Co-Authored-By: Codex <noreply@openai.com>"
```

---

### Task 4: ShopeeAutoArrangeProcessor

**Files:**
- Create: `src/Wrapsfer.Application/Shopee/Services/ShopeeAutoArrangeProcessor.cs`
- Create: `src/Wrapsfer.Application/Shopee/Services/ShopeeAutoArrangeRunSummary.cs`
- Modify: `src/Wrapsfer.Application/DependencyInjection.cs`
- Test: `tests/Wrapsfer.Application.Tests/Shopee/ShopeeAutoArrangeProcessorTests.cs`

**Interfaces:**
- Consumes: `IFulfillmentDelegationRepository.ListAsync(FulfillmentDelegationStatus?, CancellationToken)`; `IShopeeOrderRepository.ListAsync(tenantId, status, search, page, pageSize, ct)` returning `(IReadOnlyList<ShopeeOrder> Items, int TotalCount)` and `GetByIdAsync(Guid, string, CancellationToken)`; `ShopeeOrderIngestionService.IngestOrderAsync(connection, orderSn, ct)`; `ShopeeOrderArrangeService.ArrangeAsync(...)` (Task 3); `ShopeeAutoArrangeParamSelector.Select(...)` (Task 2); `ShopeeConnectionTokenRefresher.RefreshIfNeededAsync(connection, ct)`; `FulfillmentDelegation.TenantId` / `.DefaultShippingMethod`.
- Produces: `public sealed class ShopeeAutoArrangeProcessor` with `public const string SystemActor = "system:auto-arrange"` and `Task<ShopeeAutoArrangeRunSummary> RunAsync(int orderBatchSize, CancellationToken cancellationToken)`; `public sealed record ShopeeAutoArrangeRunSummary(int TenantsExamined, int RelinkAttempts, int OrdersArranged, int OrdersFailed, int OrdersSkipped)`. Task 5's job calls `RunAsync`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Wrapsfer.Application.Tests/Shopee/ShopeeAutoArrangeProcessorTests.cs`. The processor takes concrete service classes; construct them with mocked leaf dependencies (same pattern as Tasks 1 and 3 — reuse those exact constructor shapes):

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee;

public sealed class ShopeeAutoArrangeProcessorTests
{
    private const string TenantId = "tenant-a";
    private const int BatchSize = 50;
    private static readonly DateTime Now = new(2026, 7, 24, 8, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IFulfillmentDelegationRepository> _delegationRepository = new();
    private readonly Mock<IShopeeShopConnectionRepository> _connectionRepository = new();
    private readonly Mock<IShopeeOrderRepository> _orderRepository = new();
    private readonly Mock<IShopeeProductLinkRepository> _linkRepository = new();
    private readonly Mock<IShopeeGateway> _gateway = new();
    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IProductComponentRepository> _productComponentRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ShopeeAutoArrangeProcessor _processor;

    public ShopeeAutoArrangeProcessorTests()
    {
        StockReservationService stockReservationService =
            new(_productRepository.Object, _productComponentRepository.Object);
        ShopeeOrderCancellationService cancellationService = new(
            _waybillRepository.Object, stockReservationService,
            NullLogger<ShopeeOrderCancellationService>.Instance);
        ShopeeOrderIngestionService ingestionService = new(
            _gateway.Object, _orderRepository.Object, _linkRepository.Object,
            _delegationRepository.Object, _productRepository.Object,
            cancellationService, _unitOfWork.Object,
            NullLogger<ShopeeOrderIngestionService>.Instance);
        ShopeeOrderShipmentCompletionService completionService = new(
            _waybillRepository.Object, _productRepository.Object, stockReservationService,
            _unitOfWork.Object, NullLogger<ShopeeOrderShipmentCompletionService>.Instance);
        ShopeeOrderArrangeService arrangeService = new(
            _gateway.Object, completionService, _unitOfWork.Object);
        ShopeeConnectionTokenRefresher tokenRefresher = new(
            _gateway.Object, _unitOfWork.Object,
            NullLogger<ShopeeConnectionTokenRefresher>.Instance);

        _processor = new ShopeeAutoArrangeProcessor(
            _delegationRepository.Object,
            _connectionRepository.Object,
            _orderRepository.Object,
            ingestionService,
            arrangeService,
            tokenRefresher,
            _gateway.Object,
            _unitOfWork.Object,
            NullLogger<ShopeeAutoArrangeProcessor>.Instance);
    }

    private static FulfillmentDelegation CreateActiveDelegation()
    {
        FulfillmentDelegation delegation =
            FulfillmentDelegation.Request(TenantId, FulfillmentShippingMethod.Dropoff).Value;
        delegation.Accept();
        return delegation;
    }

    private static ShopeeShopConnection CreateConnection() => ShopeeShopConnection.Create(
        TenantId, 1001, "access-token", "refresh-token",
        Now.AddHours(1), Now.AddDays(30), Now, "linker").Value;

    private static ShopeeOrder CreateOrder(string orderSn, DateTime? shipBy)
    {
        ShopeeOrderSnapshot snapshot = new(
            "READY_TO_SHIP", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", shipBy);
        return ShopeeOrder.Create(TenantId, orderSn, "MY", snapshot,
            [new ShopeeOrderItemSnapshot(111, 0, "Item", null, null, 2, Guid.NewGuid())], Now).Value;
    }

    private void SetupTenant(params ShopeeOrder[] readyOrders)
    {
        _delegationRepository
            .Setup(r => r.ListAsync(FulfillmentDelegationStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FulfillmentDelegation> { CreateActiveDelegation() });
        _connectionRepository
            .Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConnection());
        _orderRepository
            .Setup(r => r.ListAsync(TenantId, ShopeeOrderStatus.NeedsLinking, null, 1, BatchSize,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ShopeeOrder>(), 0));
        _orderRepository
            .Setup(r => r.ListAsync(TenantId, ShopeeOrderStatus.ReadyToShip, null, 1, BatchSize,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((readyOrders.ToList(), readyOrders.Length));
        foreach (ShopeeOrder order in readyOrders)
        {
            _orderRepository
                .Setup(r => r.GetByIdAsync(order.Id, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);
        }
    }

    [Fact]
    public async Task RunAsync_NoActiveDelegations_DoesNothing()
    {
        _delegationRepository
            .Setup(r => r.ListAsync(FulfillmentDelegationStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FulfillmentDelegation>());

        ShopeeAutoArrangeRunSummary summary = await _processor.RunAsync(BatchSize, CancellationToken.None);

        summary.TenantsExamined.Should().Be(0);
        _connectionRepository.Verify(
            r => r.GetByTenantIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_TenantWithoutConnection_IsSkipped()
    {
        _delegationRepository
            .Setup(r => r.ListAsync(FulfillmentDelegationStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FulfillmentDelegation> { CreateActiveDelegation() });
        _connectionRepository
            .Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopeeShopConnection?)null);

        ShopeeAutoArrangeRunSummary summary = await _processor.RunAsync(BatchSize, CancellationToken.None);

        summary.TenantsExamined.Should().Be(1);
        summary.OrdersArranged.Should().Be(0);
        _orderRepository.Verify(r => r.ListAsync(It.IsAny<string>(), It.IsAny<ShopeeOrderStatus?>(),
            It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_ReadyOrder_ArrangesWithSystemActor()
    {
        ShopeeOrder order = CreateOrder("SN1", shipBy: Now.AddDays(2));
        SetupTenant(order);
        _gateway
            .Setup(g => g.GetShippingParameterAsync(1001, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeShippingParameter>.Success(
                new ShopeeShippingParameter(false, true, [], [])));
        _gateway
            .Setup(g => g.ShipOrderAsync(1001, "access-token",
                It.Is<ShopeeShipOrderRequest>(r => r.OrderSn == "SN1" && r.Dropoff != null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _gateway
            .Setup(g => g.GetTrackingNumberAsync(1001, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string?>.Success(null));

        ShopeeAutoArrangeRunSummary summary = await _processor.RunAsync(BatchSize, CancellationToken.None);

        summary.OrdersArranged.Should().Be(1);
        order.Status.Should().Be(ShopeeOrderStatus.AwaitingTracking);
        order.ShipmentArrangedBy.Should().Be(ShopeeAutoArrangeProcessor.SystemActor);
    }

    [Fact]
    public async Task RunAsync_OrderPastShipBy_IsSkippedWithoutGatewayCalls()
    {
        ShopeeOrder order = CreateOrder("SN1", shipBy: Now.AddDays(-1));
        SetupTenant(order);

        ShopeeAutoArrangeRunSummary summary = await _processor.RunAsync(BatchSize, CancellationToken.None);

        summary.OrdersSkipped.Should().Be(1);
        summary.OrdersArranged.Should().Be(0);
        order.Status.Should().Be(ShopeeOrderStatus.ReadyToShip);
        _gateway.Verify(g => g.GetShippingParameterAsync(It.IsAny<long>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_ParamFetchFails_LeavesOrderReadyToShip()
    {
        ShopeeOrder order = CreateOrder("SN1", shipBy: Now.AddDays(2));
        SetupTenant(order);
        _gateway
            .Setup(g => g.GetShippingParameterAsync(1001, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeShippingParameter>.Failure(
                new Error("Shopee.ServerError", "boom", ErrorType.Failure)));

        ShopeeAutoArrangeRunSummary summary = await _processor.RunAsync(BatchSize, CancellationToken.None);

        summary.OrdersFailed.Should().Be(0);
        order.Status.Should().Be(ShopeeOrderStatus.ReadyToShip);
        _gateway.Verify(g => g.ShipOrderAsync(It.IsAny<long>(), It.IsAny<string>(),
            It.IsAny<ShopeeShipOrderRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_NoUsableShippingOption_MarksShipmentFailed()
    {
        ShopeeOrder order = CreateOrder("SN1", shipBy: Now.AddDays(2));
        SetupTenant(order);
        _gateway
            .Setup(g => g.GetShippingParameterAsync(1001, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeShippingParameter>.Success(
                new ShopeeShippingParameter(false, false, [], [])));

        ShopeeAutoArrangeRunSummary summary = await _processor.RunAsync(BatchSize, CancellationToken.None);

        summary.OrdersFailed.Should().Be(1);
        order.Status.Should().Be(ShopeeOrderStatus.ShipmentFailed);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunAsync_NeedsLinkingOrder_GoesThroughIngestion()
    {
        ShopeeOrder needsLinking = ShopeeOrder.Create(TenantId, "SN2", "MY",
            new ShopeeOrderSnapshot("READY_TO_SHIP", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX",
                Now.AddDays(2)),
            [new ShopeeOrderItemSnapshot(111, 0, "Item", null, null, 2, null)], Now).Value;
        SetupTenant();
        _orderRepository
            .Setup(r => r.ListAsync(TenantId, ShopeeOrderStatus.NeedsLinking, null, 1, BatchSize,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ShopeeOrder> { needsLinking }, 1));
        _gateway
            .Setup(g => g.GetOrderDetailAsync(1001, "access-token", "SN2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderDetail>.Failure(
                new Error("Shopee.ServerError", "boom", ErrorType.Failure)));

        ShopeeAutoArrangeRunSummary summary = await _processor.RunAsync(BatchSize, CancellationToken.None);

        summary.RelinkAttempts.Should().Be(1);
        _gateway.Verify(g => g.GetOrderDetailAsync(1001, "access-token", "SN2",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

The connection's token expiry is in the future (`Now.AddHours(1)`), so `RefreshIfNeededAsync` performs no gateway call. If `ShopeeOrderSnapshot`'s positional shape differs from the above, copy the exact working shape from `ShopeeOrderIngestionServiceTests.cs` / `ShipShopeeOrderCommandHandlerTests.cs`.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet build 2>&1 | tail -n 5`
Expected: build FAILS — `ShopeeAutoArrangeProcessor` and `ShopeeAutoArrangeRunSummary` do not exist.

- [ ] **Step 3: Implement summary record and processor**

Create `src/Wrapsfer.Application/Shopee/Services/ShopeeAutoArrangeRunSummary.cs`:

```csharp
namespace Wrapsfer.Application.Shopee.Services;

public sealed record ShopeeAutoArrangeRunSummary(
    int TenantsExamined,
    int RelinkAttempts,
    int OrdersArranged,
    int OrdersFailed,
    int OrdersSkipped);
```

Create `src/Wrapsfer.Application/Shopee/Services/ShopeeAutoArrangeProcessor.cs`:

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
/// Sweeps tenants with an Active fulfillment delegation: re-ingests NeedsLinking orders
/// so late-added products/links unblock them, then arranges shipment for ReadyToShip
/// orders using the delegation's preferred method with fallback. Transient failures
/// leave orders ReadyToShip for the next cycle; only a missing concrete shipping option
/// or Shopee's rejection of ship_order marks ShipmentFailed. Orders past their ship-by
/// date are skipped — Shopee rejects them and the partner UI surfaces them as overdue.
/// </summary>
public sealed class ShopeeAutoArrangeProcessor(
    IFulfillmentDelegationRepository delegationRepository,
    IShopeeShopConnectionRepository connectionRepository,
    IShopeeOrderRepository orderRepository,
    ShopeeOrderIngestionService ingestionService,
    ShopeeOrderArrangeService arrangeService,
    ShopeeConnectionTokenRefresher tokenRefresher,
    IShopeeGateway shopeeGateway,
    IUnitOfWork unitOfWork,
    ILogger<ShopeeAutoArrangeProcessor> logger)
{
    public const string SystemActor = "system:auto-arrange";

    public async Task<ShopeeAutoArrangeRunSummary> RunAsync(
        int orderBatchSize, CancellationToken cancellationToken)
    {
        int tenantsExamined = 0;
        int relinkAttempts = 0;
        int ordersArranged = 0;
        int ordersFailed = 0;
        int ordersSkipped = 0;

        IReadOnlyList<FulfillmentDelegation> delegations = await delegationRepository.ListAsync(
            FulfillmentDelegationStatus.Active, cancellationToken);

        foreach (FulfillmentDelegation delegation in delegations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            tenantsExamined++;
            try
            {
                (int relinked, int arranged, int failed, int skipped) = await ProcessTenantAsync(
                    delegation, orderBatchSize, cancellationToken);
                relinkAttempts += relinked;
                ordersArranged += arranged;
                ordersFailed += failed;
                ordersSkipped += skipped;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Auto-arrange sweep failed for tenant {TenantId}", delegation.TenantId);
                unitOfWork.ClearChangeTracker();
            }
        }

        ShopeeAutoArrangeRunSummary summary = new(
            tenantsExamined, relinkAttempts, ordersArranged, ordersFailed, ordersSkipped);
        logger.LogInformation(
            "Auto-arrange run: {Tenants} tenants, {Relinks} relink attempts, {Arranged} arranged, "
            + "{Failed} failed, {Skipped} skipped",
            summary.TenantsExamined, summary.RelinkAttempts, summary.OrdersArranged,
            summary.OrdersFailed, summary.OrdersSkipped);
        return summary;
    }

    private async Task<(int Relinked, int Arranged, int Failed, int Skipped)> ProcessTenantAsync(
        FulfillmentDelegation delegation, int orderBatchSize, CancellationToken cancellationToken)
    {
        ShopeeShopConnection? connection = await connectionRepository.GetByTenantIdAsync(
            delegation.TenantId, cancellationToken);
        if (connection is null)
        {
            logger.LogWarning(
                "Auto-arrange: tenant {TenantId} has an active delegation but no Shopee connection",
                delegation.TenantId);
            return (0, 0, 0, 0);
        }

        await tokenRefresher.RefreshIfNeededAsync(connection, cancellationToken);

        int relinked = await RelinkNeedsLinkingOrdersAsync(connection, orderBatchSize, cancellationToken);

        (int arranged, int failed, int skipped) = await ArrangeReadyOrdersAsync(
            delegation, connection, orderBatchSize, cancellationToken);
        return (relinked, arranged, failed, skipped);
    }

    private async Task<int> RelinkNeedsLinkingOrdersAsync(
        ShopeeShopConnection connection, int orderBatchSize, CancellationToken cancellationToken)
    {
        (IReadOnlyList<ShopeeOrder> orders, _) = await orderRepository.ListAsync(
            connection.TenantId, ShopeeOrderStatus.NeedsLinking, null, 1, orderBatchSize,
            cancellationToken);

        int attempts = 0;
        foreach (ShopeeOrder order in orders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts++;
            Result ingestResult = await ingestionService.IngestOrderAsync(
                connection, order.OrderSn, cancellationToken);
            if (ingestResult.IsFailure)
            {
                logger.LogWarning(
                    "Auto-arrange relink of order {OrderSn} failed for tenant {TenantId}: {ErrorCode}",
                    order.OrderSn, connection.TenantId, ingestResult.Error.Code);
            }
        }

        return attempts;
    }

    private async Task<(int Arranged, int Failed, int Skipped)> ArrangeReadyOrdersAsync(
        FulfillmentDelegation delegation, ShopeeShopConnection connection, int orderBatchSize,
        CancellationToken cancellationToken)
    {
        // Listed AFTER the relink pass so orders it just unblocked arrange in this cycle.
        (IReadOnlyList<ShopeeOrder> listedOrders, _) = await orderRepository.ListAsync(
            connection.TenantId, ShopeeOrderStatus.ReadyToShip, null, 1, orderBatchSize,
            cancellationToken);

        int arranged = 0;
        int failed = 0;
        int skipped = 0;
        foreach (ShopeeOrder listedOrder in listedOrders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Re-fetch per order: a ClearChangeTracker during the relink pass (ingestion
            // failure path) detaches list results, and mutating a detached order would
            // silently not persist. Same pattern as the reconciliation processor.
            ShopeeOrder? order = await orderRepository.GetByIdAsync(
                listedOrder.Id, connection.TenantId, cancellationToken);
            if (order is null || order.Status != ShopeeOrderStatus.ReadyToShip)
            {
                continue;
            }

            if (order.ShipByDate is DateTime shipBy && shipBy < DateTime.UtcNow)
            {
                skipped++;
                continue;
            }

            Result<ShopeeShippingParameter> parameterResult = await shopeeGateway.GetShippingParameterAsync(
                connection.ShopId, connection.AccessToken, order.OrderSn, cancellationToken);
            if (parameterResult.IsFailure)
            {
                // Transient: leave ReadyToShip; the next cycle retries until ship-by passes.
                logger.LogWarning(
                    "Auto-arrange shipping-parameter fetch failed for order {OrderSn}: {ErrorCode}",
                    order.OrderSn, parameterResult.Error.Code);
                continue;
            }

            Result<ShopeeShipOrderRequest> selection = ShopeeAutoArrangeParamSelector.Select(
                order.OrderSn, delegation.DefaultShippingMethod, parameterResult.Value);
            if (selection.IsFailure)
            {
                Result failResult = order.MarkShipmentFailed(
                    selection.Error.Description, DateTime.UtcNow);
                if (failResult.IsSuccess)
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                failed++;
                continue;
            }

            Result arrangeResult = await arrangeService.ArrangeAsync(
                order, connection, selection.Value, SystemActor, cancellationToken);
            if (arrangeResult.IsSuccess)
            {
                arranged++;
            }
            else
            {
                failed++;
                logger.LogWarning(
                    "Auto-arrange failed for order {OrderSn} of tenant {TenantId}: {ErrorCode}",
                    order.OrderSn, connection.TenantId, arrangeResult.Error.Code);
            }
        }

        return (arranged, failed, skipped);
    }
}
```

Register in `src/Wrapsfer.Application/DependencyInjection.cs` after the `ShopeeOrderReconciliationProcessor` line:

```csharp
services.AddScoped<ShopeeAutoArrangeProcessor>();
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build 2>&1 | tail -n 3 && dotnet test --no-restore --no-build --filter "FullyQualifiedName~ShopeeAutoArrangeProcessorTests" 2>&1 | tail -n 5`
Expected: PASS, 7/7.

- [ ] **Step 5: Full Application suite**

Run: `dotnet test --no-restore --no-build tests/Wrapsfer.Application.Tests 2>&1 | tail -n 3`
Expected: `Failed: 15` baseline, nothing new.

- [ ] **Step 6: Format and commit**

```bash
make format-all
git add -A
git commit -m "feat(shopee): add auto-arrange processor for delegated tenants

Per Active delegation each cycle: refresh the connection token, re-ingest
NeedsLinking orders so late-added links unblock them, then arrange every
ReadyToShip order not past ship-by via the shared arrange service as
system:auto-arrange. Transient errors retry next cycle; only a missing
concrete shipping option or Shopee rejection marks ShipmentFailed.

Co-Authored-By: Codex <noreply@openai.com>"
```

---

### Task 5: Options, background job, DI, config

**Files:**
- Create: `src/Wrapsfer.Infrastructure/Shopee/ShopeeAutoArrangeOptions.cs`
- Modify: `src/Wrapsfer.Infrastructure/Shopee/ShopeeOptions.cs`
- Create: `src/Wrapsfer.Infrastructure/BackgroundServices/ShopeeAutoArrangeJob.cs`
- Modify: `src/Wrapsfer.Infrastructure/DependencyInjection.cs`
- Modify: `src/Wrapsfer.Api/appsettings.json` (and `src/Wrapsfer.Api/appsettings.Development.json` if it has a `Shopee` section)

**Interfaces:**
- Consumes: `ShopeeAutoArrangeProcessor.RunAsync(int, CancellationToken)` (Task 4); the `ShopeeOrderReconciliationJob` pattern.
- Produces: hosted service wired into DI; config section `Shopee:AutoArrange` with `Enabled` (default false), `IntervalMinutes` (default 5, floor 1), `OrderBatchSize` (default 100).

- [ ] **Step 1: Create the options class**

`src/Wrapsfer.Infrastructure/Shopee/ShopeeAutoArrangeOptions.cs`:

```csharp
namespace Wrapsfer.Infrastructure.Shopee;

public sealed class ShopeeAutoArrangeOptions
{
    public bool Enabled { get; set; }
    public int IntervalMinutes { get; set; } = 5;
    public int OrderBatchSize { get; set; } = 100;
}
```

Add to `ShopeeOptions` (after the `OrderSync` property):

```csharp
public ShopeeAutoArrangeOptions AutoArrange { get; set; } = new();
```

- [ ] **Step 2: Create the job**

`src/Wrapsfer.Infrastructure/BackgroundServices/ShopeeAutoArrangeJob.cs` (mirrors `ShopeeOrderReconciliationJob` exactly):

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Infrastructure.Shopee;

namespace Wrapsfer.Infrastructure.BackgroundServices;

public sealed class ShopeeAutoArrangeJob(
    IServiceScopeFactory scopeFactory,
    IOptions<ShopeeOptions> options,
    ILogger<ShopeeAutoArrangeJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ShopeeAutoArrangeOptions autoArrangeOptions = options.Value.AutoArrange;
        if (!autoArrangeOptions.Enabled)
        {
            logger.LogInformation("Shopee auto-arrange job is disabled by configuration");
            return;
        }

        int intervalMinutes = Math.Max(autoArrangeOptions.IntervalMinutes, 1);
        using PeriodicTimer timer = new(TimeSpan.FromMinutes(intervalMinutes));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ExecuteOnceAsync(autoArrangeOptions, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }

    private async Task ExecuteOnceAsync(
        ShopeeAutoArrangeOptions autoArrangeOptions, CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ShopeeAutoArrangeProcessor processor =
                scope.ServiceProvider.GetRequiredService<ShopeeAutoArrangeProcessor>();

            await processor.RunAsync(autoArrangeOptions.OrderBatchSize, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // graceful shutdown mid-run
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ShopeeAutoArrangeJob iteration failed");
        }
    }
}
```

- [ ] **Step 3: Register and configure**

In `src/Wrapsfer.Infrastructure/DependencyInjection.cs`, after `services.AddHostedService<ShopeeOrderReconciliationJob>();`:

```csharp
services.AddHostedService<ShopeeAutoArrangeJob>();
```

In `src/Wrapsfer.Api/appsettings.json`, inside the `"Shopee"` object after `"OrderSync"`:

```json
"AutoArrange": {
    "Enabled": false
}
```

Check `src/Wrapsfer.Api/appsettings.Development.json`: if it contains a `"Shopee"` section, add the same `"AutoArrange"` block there with `"Enabled": true` ONLY if that file already enables `OrderSync`; otherwise mirror `"Enabled": false`.

- [ ] **Step 4: Build, full test run, boot check**

Run: `dotnet build 2>&1 | tail -n 3 && dotnet test --no-restore --no-build 2>&1 | tail -n 6`
Expected: build succeeds; `Wrapsfer.Application.Tests` shows the `Failed: 15` baseline; Domain and other suites fully pass.

Boot check (verifies DI wiring; requires local infra `docker compose -f compose.dev.yaml up -d` if not already running):

Run: `timeout 25 dotnet run --project src/Wrapsfer.Api 2>&1 | grep -iE "auto-arrange|Now listening|fail|error" | head -n 10`
Expected: the line `Shopee auto-arrange job is disabled by configuration` (or, with Development enabling it, no such line) and `Now listening on ...`; no DI resolution errors. If infra is unavailable and the app cannot boot for unrelated reasons, note that and rely on the build + tests.

- [ ] **Step 5: Format and commit**

```bash
make format-all
git add -A
git commit -m "feat(shopee): schedule auto-arrange background job

PeriodicTimer job (Shopee:AutoArrange config, disabled by default)
driving ShopeeAutoArrangeProcessor per cycle, following the order
reconciliation job pattern.

Co-Authored-By: Codex <noreply@openai.com>"
```

---

### Task 6: Final verification

- [ ] **Step 1: Full clean verification**

```bash
make restore build test 2>&1 | tail -n 10
```
Expected: build clean; only the 15 known `ShopeeConnectionTokenRefresher` NRE failures in `Wrapsfer.Application.Tests`; all new test classes (`ShopeeAutoArrangeParamSelectorTests`, `ShopeeAutoArrangeProcessorTests`, `ShopeeOrderArrangeServiceTests`, the 5 new `ShopeeOrderIngestionServiceTests` cases) green.

- [ ] **Step 2: Confirm the working tree is clean and the log is exactly 5 new commits**

```bash
git status --short && git log --oneline -7
```
Expected: empty status; the 5 task commits on top of `bd4d0da docs(fulfillment-delegation): phase 2 auto-match and auto-arrange design`.

Do NOT push.
