# Shopee Order → Waybill Sync — Design

**Date:** 2026-07-16
**Status:** Approved
**Depends on:** Existing Shopee integration (`ShopeeShopConnection`, `ShopeeProductLink`, `IShopeeGateway`), existing `Waybill` aggregate.

## Goal

Bridge Shopee orders into Wrapsfer fulfillment. Shopee orders sync into Wrapsfer automatically (webhook push + reconciliation poll). The user arranges shipment from Wrapsfer (per-order modal), Wrapsfer obtains the tracking number from Shopee, creates the Wrapsfer `Waybill` from the order, and serves the Shopee AWB label PDF for printing. Cancellations sync back and auto-cancel the linked waybill when still cancellable.

## Decisions (from brainstorming)

| Decision | Choice |
|---|---|
| Sync model | Auto-sync orders into Wrapsfer; waybill created automatically once tracking number is known |
| Shipping arrangement | Wrapsfer calls Shopee (`ship_order`) — user-triggered per order, not automatic |
| Ship options UX | Modal per order showing Shopee's pickup/drop-off options (tenant defaults / auto-arrange deferred) |
| AWB label | Printed from Wrapsfer; PDF persisted on first download (Shopee deletes documents post-shipment); printed indicator on order |
| Unlinked items | Block shipment until all order items are linked to Wrapsfer products (reuse existing link/create drawers) |
| Cancellations | Synced; linked waybill auto-cancelled if still Draft/Packed |
| Transport | Webhooks primary + hourly reconciliation poll as safety net |

Validated against SiteGiant's Shopee integration: our v1 equals their "Manual Select" arrange mode; their Pickup/Dropoff defaults and Auto Arrange scheduler are the proven future upgrade path. Label persistence and the printed indicator are adopted from their design.

## Architecture & data flow

```
Shopee push ──► POST api/v1/shopee/webhook ──► ShopeeWebhookEvent (stored, deduped)
                                                     │
reconciliation job (hourly) ─────────────────────────┤
                                                     ▼
                                            order sync processor
                                            (get_order_detail → upsert ShopeeOrder,
                                             resolve items vs ShopeeProductLinks)
                                                     │
         ┌───────────────────────────────────────────┼──────────────────────────┐
         ▼                                           ▼                          ▼
 NeedsLinking ──user links + relink──► ReadyToShip ──user ships──► AwaitingTracking
                                                                         │ tracking number arrives
                                                                         ▼
                                                             Waybill created + linked,
                                                             order Shipped, label downloadable
```

Cancellation pushes at any point mark the order `Cancelled`; a linked waybill still in `Draft`/`Packed` is cancelled via the existing `Waybill.Cancel` flow (restores reserved stock). A waybill already `HandedOff` is not touched — the order is flagged for attention instead.

## Domain model

### `ShopeeOrder` (aggregate root, table `ShopeeOrders`)

- **Identity:** `Id`, `TenantId`, `OrderSn`. Unique index `(TenantId, OrderSn)`.
- **Shopee snapshot:** `ShopeeStatus` (raw string: `READY_TO_SHIP`, `PROCESSED`, `SHIPPED`, `COMPLETED`, `CANCELLED`, `IN_CANCEL`), `Region`, `BuyerUsername`, recipient name/phone/address snapshot, `TotalAmount` + currency, `CodAmount` (nullable), `ShippingCarrier`, `ShipByDate`.
- **Internal status enum** (drives UI and allowed actions):
  `NeedsLinking → ReadyToShip → AwaitingTracking → Shipped` | `Cancelled` (terminal) | `ShipmentFailed` (retryable → `ReadyToShip`).
- **Fulfillment link:** `TrackingNumber` (nullable), `WaybillId` (nullable FK to `Waybills`), `ShipmentArrangedAt`, `ShipmentArrangedBy`.
- **Label:** `LabelStorageKey` (nullable), `LabelPrintedAt` (nullable, set on first download).
- **Bookkeeping:** `LastSyncedAt`, `LastShipError`, `xmin` concurrency token.
- **Behavior:** `Create(...)`, `ApplyShopeeSnapshot(...)` (upsert path; re-resolves items and derives internal status), `MarkShipmentArranged(...)`, `AssignTracking(...)`, `LinkWaybill(...)`, `MarkShipmentFailed(error)`, `MarkCancelled()`, `MarkLabelStored(key)` / `MarkLabelPrinted()`. Illegal transitions rejected with domain errors (`ShopeeOrderErrors.cs`).

Internal status is *derived* on every sync: an order in `NeedsLinking` becomes `ReadyToShip` automatically once all items resolve to product links.

### `ShopeeOrderItem` (child collection)

`ShopeeItemId`, `ShopeeModelId` (0 = model-less), `ItemName`/`ModelName`/`ItemSku` snapshot, `Quantity`, resolution result `ProductId` + `ProductBarcode` (both nullable; null = unlinked → parent is `NeedsLinking`). Resolution re-runs on every sync and on explicit relink.

### `ShopeeWebhookEvent` (dedup/audit — mirrors `StripeWebhookEvent`)

`ShopId`, push `Code`, Shopee message id + timestamp (dedup key), raw JSON payload, `Status` (`Pending`/`Processed`/`Failed`/`Ignored`), `ProcessedAt`, `Error`, retry count.

### Explicitly unchanged

The `Waybill` aggregate is not modified. Link direction is `ShopeeOrder.WaybillId → Waybill`; waybills stay marketplace-agnostic. New repos `IShopeeOrderRepository`, `IShopeeWebhookEventRepository` follow existing interface patterns; EF configurations + migration follow existing conventions (tenant filtering, `xmin`).

## Sync pipeline

### Gateway additions (`IShopeeGateway` / `ShopeeHttpGateway`)

Same signing (`ShopeeRequestSigner`) and DTO patterns:

- `/api/v2/order/get_order_list` — reconciliation, windowed by `update_time`, cursor-paged
- `/api/v2/order/get_order_detail` — full order incl. items + recipient
- `/api/v2/logistics/get_shipping_parameter`
- `/api/v2/logistics/ship_order`
- `/api/v2/logistics/get_tracking_number`
- `/api/v2/logistics/create_shipping_document`
- `/api/v2/logistics/download_shipping_document`

### Webhook endpoint

`POST api/v1/shopee/webhook` on `ShopeeController`, `[AllowAnonymous]` (like the OAuth callback). Request path does only: verify Shopee push signature (HMAC-SHA256 of `url|body` with partner key — new method on `ShopeeRequestSigner`), dedup on Shopee message identity, insert `ShopeeWebhookEvent`, return 200. No business logic inline — Shopee disables pushes for slow endpoints. Handled push codes: **order status push** and **tracking number push**; all other codes stored as `Ignored`.

### Event consumer

Hosted background service (pattern: `ShopeeTokenRefreshJob`) drains `Pending` events in order per shop:

1. Resolve `ShopId` → `ShopeeShopConnection` → tenant (unknown shop → mark `Failed`, no retry).
2. Order-status events: `get_order_detail` → `ApplyShopeeSnapshot` upsert.
3. Tracking-number events: route to ship-completion path (below).
4. Failure: mark `Failed` with error, retry with backoff, cap at 5 attempts; reconciliation heals the rest.

### Scope filter

Orders are only *created* at `READY_TO_SHIP` or later. `UNPAID` pushes ignored. After creation, all status changes tracked. `IN_CANCEL` shows a warning in the UI and blocks arranging new shipment, but cancels nothing; only `CANCELLED` triggers cancellation handling.

### Reconciliation job

Hourly per connected shop (pattern: `ShopeeStockSyncJob`; config `ShopeeOrderSyncOptions` — interval, window, backoff caps):

- `get_order_list` filtered by `update_time` over a sliding 24h window, diff vs stored orders, feed the same `get_order_detail` → `ApplyShopeeSnapshot` path.
- Re-poll `get_tracking_number` for orders stuck in `AwaitingTracking` longer than a configurable threshold (`ShopeeOrderSyncOptions`, default 30 minutes).

### Operational setup (documented, manual)

- Webhook URL is registered per partner app in the Shopee Open Platform console (not via API).
- Local development: expose via the existing `ngrok.yml` tunnel.

## Ship flow, labels, cancellation

### Arrange shipment (user-triggered)

1. `GET .../orders/{orderId}/shipping-parameter` → Shopee `get_shipping_parameter` → modal data (pickup: address list + time slots; drop-off: optional branch list).
2. `POST .../orders/{orderId}/ship` with chosen method + slot/address. Handler validates status `ReadyToShip` (all items linked), calls `ship_order`, transitions to `AwaitingTracking`. Shopee rejection → `ShipmentFailed` + `LastShipError`, retryable from UI.
3. **Completion (async):** tracking-number push or reconciliation re-poll triggers the completion handler, which atomically: assigns tracking number; creates the `Waybill` via existing `Waybill.Create` (packaging date = server date (UTC) at completion time, `WaybillNumber` = tracking number, items from resolved `ProductId`/`ProductBarcode`/`Quantity`); links via `LinkWaybill`; marks order `Shipped`. Tracking number colliding with an existing waybill number → order surfaces an error state (no silent failure).

### Labels

`GET .../orders/{orderId}/label` (shipped orders only). First request: `create_shipping_document` + `download_shipping_document`, persist PDF via existing `IReportStorage`, record `LabelStorageKey`, set `LabelPrintedAt`, stream PDF. Subsequent requests serve the stored copy (Shopee deletes shipping documents after shipment). Re-download always allowed; `LabelPrintedAt` drives a "printed" indicator to prevent accidental double-printing.

### Cancellation

`CANCELLED` (push or reconciliation) → `MarkCancelled()`:

- Linked waybill in `Draft`/`Packed` → cancel via existing `Waybill.Cancel` with reason "Shopee order cancelled" (restores reserved stock like manual cancellation).
- Waybill `HandedOff` → order flagged needs-attention only; physical recall out of scope.
- No waybill yet → order simply becomes `Cancelled`.

### Concurrency

Ship command and sync processor may race (e.g., cancellation push mid-ship). Both go through guarded aggregate transitions + `xmin` token: sync side retries on conflict; user action rejected with a fresh-state error.

## API surface

On `ShopeeController` (`PartnerIntegrationAdmin` policy, `{tenantId}` scoped — except the anonymous webhook):

| Endpoint | Purpose |
|---|---|
| `POST api/v1/shopee/webhook` | Anonymous, signature-verified push receiver |
| `GET {tenantId}/orders` | Paged list; filter by internal status; search by order SN / tracking number |
| `GET {tenantId}/orders/{orderId}` | Detail incl. items with link resolution |
| `GET {tenantId}/orders/{orderId}/shipping-parameter` | Modal data |
| `POST {tenantId}/orders/{orderId}/ship` | Arrange shipment |
| `POST {tenantId}/orders/{orderId}/relink` | Re-resolve item links on demand after user links a product |
| `GET {tenantId}/orders/{orderId}/label` | AWB PDF (store-on-first-fetch, then serve stored) |

Application layer (MediatR + FluentValidation + `Result<T>`): commands `ShipShopeeOrder`, `RelinkShopeeOrderItems`; queries `GetShopeeOrders`, `GetShopeeOrderDetail`, `GetShopeeShippingParameter`, `GetShopeeOrderLabel`; internal sync/completion handlers for webhook + reconciliation processing.

## Frontend (partner portal)

New feature `partner/src/features/partner/shopee-orders/` (mirrors `shopee-products/`), route `partner/src/routes/partner/_layout/shopee-orders.tsx`:

- **Orders table** — status tabs (Needs linking / Ready to ship / Arranging / Shipped / Cancelled); columns: order SN, buyer, items summary, ship-by date, courier, tracking number, printed indicator; row expansion for items. TanStack Query with polling refetch on the Arranging tab so `AwaitingTracking → Shipped` flips without manual refresh.
- **Arrange-shipment modal** — fetches shipping parameters on open; renders pickup (address + slot picker) or drop-off per courier support; confirm → ship mutation → Arranging.
- **Needs-linking resolution** — unresolved items shown per row; "Link product" opens the existing `link-product-drawer` / `create-linked-product-drawer` (exported from `shopee-products`), then calls `relink`.
- **Label download** — shipped orders; sets printed indicator after first download.
- **Badges** — `IN_CANCEL` warning; cancelled-after-handoff attention flag; `ShipmentFailed` with retry action.

Conventions: react-intl i18n, Zod schemas, `service.ts` / `query-options.ts` / `types.ts` per `partner/CLAUDE.md`. Only change to existing features: export the two drawers for reuse.

## Error handling summary

- Webhook: signature failure → 401, no event stored. Duplicate message → 200, no-op. Processing failure → retry with backoff (cap 5), then reconciliation heals.
- Ship: Shopee rejection → `ShipmentFailed` + stored error, UI retry.
- Tracking collision with existing waybill number → surfaced error state on order.
- Unknown shop id on push → event `Failed`, no retry.
- Concurrency conflicts → sync retries; user actions rejected with fresh-state error.

## Testing

- **Domain unit tests:** `ShopeeOrder` state machine — every legal transition, every illegal transition rejected, status derivation from snapshot + link resolution.
- **Handler tests (mocked `IShopeeGateway`):** ship happy path; ship failure; tracking completion incl. waybill creation and collision; cancellation × waybill-status matrix; webhook signature rejection and dedup; reconciliation diffing.
- **Frontend (Vitest):** arrange-shipment modal option rendering, table status/tab rendering, needs-linking flow.
- **Manual E2E:** ngrok tunnel + Shopee test shop before release.

## Out of scope (v1)

- Tenant-default shipping method / auto-arrange scheduler (SiteGiant-style) — future phase.
- Bulk arrange shipment.
- Packing/picking list printing alongside labels.
- Buyer-cancellation (`IN_CANCEL`) accept/reject from Wrapsfer.
- Returns/refunds; physical recall of handed-off parcels.
- Multi-shop per tenant (current connection model is one shop per tenant).
- Encrypting stored Shopee tokens (pre-existing gap, noted separately).
