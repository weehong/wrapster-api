# Fulfillment Delegation Phase 2 — auto-match and auto-arrange (design)

Date: 2026-07-24
Status: approved
Repos touched: `api` only (branch `shopee`). No frontend or schema changes.
Prerequisite reading: `docs/superpowers/specs/2026-07-16-shopee-order-waybill-sync-design.md`, Phase 1 plan (`FulfillmentDelegation` handshake).

## Problem

Phase 1 built the delegation handshake: a partner opts in, the owner accepts, and a
`FulfillmentDelegation` row becomes `Active`. Nothing reads that status. Orders for a
delegated tenant sit in `ReadyToShip` forever because the partner has stopped clicking
"Arrange shipment" and no automation replaces them. No waybill is ever created, so the
owner platform never sees the order and has nothing to print a label from.

## Goal

For tenants with an `Active` fulfillment delegation, incoming Shopee orders should flow
to a Wrapsfer waybill with no human action: items auto-link by SKU→barcode, shipment is
auto-arranged, and the existing tracking-completion path creates the waybill the owner
already knows how to view and print (Shopee label button on waybill detail).

Out of scope (locked decisions):
- **No owner UI.** Stuck orders (unmatched items, permanent arrange failures) remain
  visible only in the partner portal until Phase 3.
- **No schema changes.** Phase 1's `FulfillmentDelegations` table and the existing
  `ShopeeOrders`/`ShopeeProductLinks` tables carry everything.
- Pre-existing failures of 15 `ShopeeConnectionTokenRefresher` NRE tests are not
  addressed here.

## Design decisions (locked with user)

1. **Strict Phase 2 scope** — automation only, no owner-facing surface.
2. **Auto-match creates a persistent `ShopeeProductLink`** — a successful SKU→barcode
   match links the Shopee item/model permanently, exactly as a manual link would.
3. **Retry until ship-by** — transient failures leave the order `ReadyToShip` and the
   next job cycle retries; only permanent conditions mark `ShipmentFailed`.
4. **Prefer + fallback param selection** — use the delegation's preferred method when
   the order supports it; fall back to the other method when it doesn't; fail only when
   no usable option exists.
5. Phase 1 work is committed before this phase starts (clean baseline).

## Architecture

Three units, all following existing patterns in the codebase:

### 1. SKU→barcode auto-match at ingestion

`ShopeeOrderIngestionService.ResolveItemsAsync` currently resolves each order line via
`ShopeeProductLink` (`(ItemId, ModelId) → ProductId`). Extension:

- After the link-table pass, if any lines are unresolved **and** the tenant's delegation
  is `Active` (via `IFulfillmentDelegationRepository.GetByTenantIdAsync`):
  - Collect the unresolved lines' non-blank `ItemSku` values, trimmed.
  - Batch-fetch products with `IProductRepository.GetByBarcodesAsync` (exact, ordinal
    match — barcodes are scanner-exact; no case folding).
  - For each hit, create and persist a `ShopeeProductLink` for `(ItemId, ModelId)` and
    resolve the line with the product id.
- Misses stay unresolved; the order lands in `NeedsLinking` as today.
- Non-delegated tenants: behavior completely unchanged (no delegation lookup cost
  beyond one indexed query per ingestion with unresolved lines).
- Duplicate-link safety: the link is only created for lines that had no link, and
  ingestion of concurrent orders for the same item is serialized per webhook/
  reconciliation processing; a unique-index violation on the link table is treated as
  benign (re-resolve from the table).

### 2. Shared arrange service (extraction refactor)

`ShipShopeeOrderCommandHandler` owns the arrange logic today and takes the arranger
from HTTP-scoped `ITenantContext.Username` — unusable in a background job. Extract an
Application service:

- `Application/Shopee/Services/ShopeeOrderArrangeService` with
  `ArrangeAsync(ShopeeOrder order, ShopeeShopConnection connection, ShopeeShipOrderRequest shipRequest, string? arrangedBy, CancellationToken ct)`
  containing, verbatim from the handler:
  - guard: status must be `ReadyToShip` or `ShipmentFailed`;
  - guard: `HasUnresolvedItems` → `ItemsNotLinked`;
  - guard: `ShopeeStatus == "IN_CANCEL"` → `CancellationRequested`;
  - resume path: `ShipmentFailed` with `ShipmentArrangedAt != null` skips ship_order
    and goes straight to completion retry;
  - guard: `ShopeeStatus` in `PROCESSED/SHIPPED/COMPLETED/TO_CONFIRM_RECEIVE` →
    `AlreadyArrangedOnShopee`;
  - `IShopeeGateway.ShipOrderAsync` → failure marks `ShipmentFailed` (existing
    manual-flow semantics preserved for the HTTP path; the job pre-filters transient
    errors, see §3);
  - `MarkShipmentArranged(arrangedBy, utcNow)` + save;
  - opportunistic `GetTrackingNumberAsync` → `ShopeeOrderShipmentCompletionService`.
- `ShipShopeeOrderCommandHandler` becomes: load order + connection, refresh token,
  build `ShopeeShipOrderRequest` from the request DTO, call
  `ArrangeAsync(..., arrangedBy: tenantContext.Username, ...)`. **The HTTP flow's
  observable behavior does not change** — same errors, same status codes, same
  side effects. Existing handler tests are the regression net; they move/point to the
  service where appropriate.
- System actor constant: `"system:auto-arrange"` (lives on the processor, passed as
  `arrangedBy`). `MarkShipmentArranged` already accepts `string?` — no domain change.

### 3. Auto-arrange background job

Clone of the `ShopeeOrderReconciliationJob` → processor pattern:

- **Infrastructure**: `BackgroundServices/ShopeeAutoArrangeJob` — `BackgroundService`
  + `PeriodicTimer`, resolves a scoped `ShopeeAutoArrangeProcessor` per tick, catches
  and logs all exceptions per iteration.
- **Config**: `ShopeeOptions.AutoArrange` (`Enabled` bool default false,
  `IntervalMinutes` int default 5, floor 1) — same shape as `OrderSync`. Add the keys
  to `appsettings*.json` alongside existing Shopee config.
- **Application**: `Services/ShopeeAutoArrangeProcessor.RunAsync(ct)`:
  1. `IFulfillmentDelegationRepository.ListAsync(Active)` → delegated tenants.
  2. Per tenant (sequential; per-tenant try/catch so one tenant cannot abort the
     sweep):
     a. Load `ShopeeShopConnection`; missing → log, skip tenant.
     b. `ShopeeConnectionTokenRefresher.RefreshIfNeededAsync` once per tenant.
     c. **Relink pass**: for the tenant's `NeedsLinking` orders, re-run item
        resolution (reuse the ingestion service's resolution path, which now includes
        auto-match) so products/links added after ingestion unblock orders.
     d. **Arrange pass**: for each `ReadyToShip` order whose `ShipByDate` has not
        passed (past-ship-by orders are skipped — Shopee rejects them and the partner
        UI already shows Overdue):
        - `IShopeeGateway.GetShippingParameterAsync(order)`.
        - Select params (§ param selection). No usable option →
          `MarkShipmentFailed("no usable shipping option", now)` (permanent).
        - Param fetch failure → log, leave `ReadyToShip` (transient; next cycle
          retries).
        - `ShopeeOrderArrangeService.ArrangeAsync(..., "system:auto-arrange", ...)`.
          Shopee's rejection of ship_order marks `ShipmentFailed` inside the service
          (permanent, consistent with manual flow).
  3. Log a run summary (tenants, orders examined, arranged, failed, skipped) —
     follow `ShopeeWebhookRunSummary`-style summary records.
- Repository support: `IShopeeOrderRepository` gains a tenant-scoped
  `ListByStatusAsync(status, tenantId, ct)` if no equivalent exists (check before
  adding; reuse whatever the reconciliation processor uses).

### Param selection (prefer + fallback)

Given `ShopeeShippingParameter(SupportsPickup, SupportsDropoff, PickupAddresses, DropoffBranches)`
and the delegation's `DefaultShippingMethod`:

- **Dropoff** chosen when: preferred=Dropoff and `SupportsDropoff`, or
  preferred=Pickup and not `SupportsPickup` but `SupportsDropoff`.
  Branch = first of `DropoffBranches`; when the list is empty, send branchless
  dropoff (`BranchId = 0`/absent — mirror what the partner UI sends for branchless
  carriers).
- **Pickup** chosen when: preferred=Pickup and `SupportsPickup`, or preferred=Dropoff
  and not `SupportsDropoff` but `SupportsPickup`.
  Address = first address flagged default, else first; slot = earliest available
  `PickupTimeId` for that address. No address or no slot → treat as "no usable
  option" (permanent failure).
- Neither method supported → permanent failure.

### Transient vs permanent

| Condition | Treatment |
|---|---|
| Token refresh failure, network error, param fetch failure | Transient — log, leave `ReadyToShip`, retry next cycle |
| Neither method supported / pickup without address or slot | Permanent — `MarkShipmentFailed` |
| `ship_order` rejected by Shopee | Permanent — `MarkShipmentFailed` (service behavior) |
| Order past `ShipByDate` | Skipped — untouched, stays visible as Overdue partner-side |
| Items still unresolved after relink pass | Untouched — stays `NeedsLinking` (`MarkShipmentFailed` is not legal from `NeedsLinking`) |

## Error handling & risks

- **Double-arrange**: guarded by the service's `ShopeeStatus` checks (`PROCESSED`/…)
  and local status machine, same as manual flow. The job additionally never touches
  orders outside `ReadyToShip`.
- **Tenant isolation in a cross-tenant job**: every repository call is tenant-scoped
  (repo methods all take `tenantId` per repo convention); processing is sequential
  per tenant with per-tenant scopes for saves.
- **Rate limits**: one `GetShippingParameterAsync` + one `ShipOrderAsync` per order per
  cycle, sequential; interval default 5 min. No parallelism in v1.
- **Waybill/stock failures after arrange**: handled today by
  `ShopeeOrderShipmentCompletionService` (marks `ShipmentFailed` with reason;
  reconciliation retries tracking).

## Testing

TDD per layer (xUnit + Moq + FluentAssertions, mirroring existing Shopee tests):

- **Ingestion auto-match**: link exists → unchanged; no link + Active delegation +
  SKU==barcode → line resolved and link persisted; blank/missing SKU → unresolved;
  no delegation / non-Active → no product lookup, unchanged behavior; barcode miss →
  unresolved.
- **Arrange service extraction**: all existing `ShipShopeeOrderCommandHandler` test
  cases still pass (guards, resume path, failure marking, opportunistic completion);
  `arrangedBy` propagates to `MarkShipmentArranged`.
- **Auto-arrange processor**: selects only Active delegations; skips tenants without
  connection; relink pass unblocks a `NeedsLinking` order; param-selection matrix
  (preferred supported / fallback / neither / branchless dropoff / default-flagged
  address / earliest slot); transient param failure leaves `ReadyToShip`; no-usable-
  option marks `ShipmentFailed`; past-ship-by skipped; one tenant throwing does not
  stop the next; summary counts.
- **Job**: config disabled → no-op (mirror reconciliation job test if one exists;
  otherwise job stays thin enough to skip direct tests, consistent with existing jobs).

Verification: `make restore build test` (the 15 pre-existing `ShopeeConnectionTokenRefresher`
NRE failures are the known baseline); `make format-all` before committing.

## Result for the original complaint

Happy path: order webhook → ingestion auto-links items → job arranges within one
interval → tracking lands → waybill created → order appears in the owner's waybill
list, where the Phase 1 Shopee label button prints the waybill. Stuck orders remain a
partner-portal concern until Phase 3 (owner fallback console).
