# Create Wrapsfer Product from Shopee Item — Design

**Date:** 2026-07-16
**Repos affected:** `api` (backend, this repo) and `partner` (frontend)
**Status:** Approved

## Problem

Partners can already connect their Shopee shop and link a Shopee item/model to an **existing** Wrapsfer product (`LinkShopeeProductCommand`). But when a Shopee listing has no Wrapsfer counterpart yet, the partner must manually create the product first (retyping name, SKU, stock) and then link it in a second step.

## Goal

Let a partner create a **new** Wrapsfer product pre-filled from a Shopee item and link it to that item in one atomic operation, directly from the Shopee products page.

## Decisions (from brainstorming)

- **Single-item flow only.** No bulk create. The action lives on each unlinked sellable-unit row in the Shopee items table.
- **Partner form for missing fields.** Shopee provides name, SKU, and stock but not barcode or cost. A drawer form is pre-filled from Shopee; the partner must enter Barcode and Cost before saving.
- **Stock seeded from Shopee.** The form pre-fills the unit's current Shopee stock (editable). Since the sync job pushes Wrapsfer stock *to* Shopee, seeding from Shopee makes the first sync a no-op.
- **Create + link is atomic.** One command creates the `Product` and the `ShopeeProductLink` in a single transaction. If any step fails, nothing is created.
- **Product Type is always `Single`.** Bundles cannot be linked to Shopee; packages need unpack configuration that does not fit this flow.

## Backend Design (api repo)

### Command

`Application/Shopee/Commands/CreateShopeeLinkedProduct/`

- `CreateShopeeLinkedProductCommand` : `ICommand<ShopeeProductLinkResponse>` carrying:
  - `TenantId`
  - `ShopeeItemId` (long), `ShopeeModelId` (long, 0 for model-less items)
  - Product fields confirmed by the partner: `Barcode`, `Name`, `SkuCode?`, `Cost` (decimal), `StockQuantity` (int), `LowStockThreshold?` (int)
- `CreateShopeeLinkedProductCommandValidator` (FluentValidation): mirrors CreateProduct rules — barcode and name required, cost ≥ 0, stock ≥ 0, low-stock threshold ≥ 0 when present — plus `ShopeeItemId > 0`, `ShopeeModelId ≥ 0`.
- `CreateShopeeLinkedProductCommandHandler`, single transaction via one `IUnitOfWork.SaveChangesAsync`:
  1. `IShopeeProductLinkRepository.ExistsAsync(tenantId, itemId, modelId)` → fail `ShopeeProductLinkErrors.AlreadyLinked`.
  2. `IShopeeShopConnectionRepository.GetByTenantIdAsync` → fail `ShopeeProductLinkErrors.ConnectionNotFound`.
  3. `ShopeeSellableUnitResolver.ResolveAsync(connection, itemId, modelId)` — validates the unit exists in the shop and returns the fresh name/SKU snapshot; propagate its errors (`ItemNotFoundInShop`, `ModelMismatch`, `ItemFetchFailed`, `AuthFailed`).
  4. Uniqueness checks, same mechanism as `CreateProductCommandHandler`: `GetByBarcodeAsync` → fail `ProductErrors.BarcodeAlreadyExists`; when SkuCode is provided, `GetBySkuCodeAsync` → fail `ProductErrors.SkuAlreadyExists` (likely collision source, since SKU is pre-filled from Shopee).
  5. `Product.Create(tenantId, barcode, name, ProductType.Single, cost, stockQuantity, skuCode, lowStockThreshold)` — partner-entered values win over Shopee values for the product record.
  6. Apply `product.CheckLowStock(fallbackThreshold)` with the tenant-settings / global-settings fallback, exactly as `CreateProductCommandHandler` does.
  7. `ShopeeProductLink.Create(...)` using the **resolver's** snapshot values (item/model name, SKU) and `tenantContext.Username` as `LinkedBy`.
  8. Add product + link, `SaveChangesAsync` once — all-or-nothing.
  9. Return `ShopeeProductLinkResponseMapper.Map(link, product)`.

No new entities, no schema changes, no migration.

### API

- `POST api/v1/shopee/product-links/with-new-product` on `ShopeeController`.
- New contract `Api/Contracts/CreateShopeeLinkedProductRequest.cs` (barcode, name, skuCode, cost, stockQuantity, lowStockThreshold, shopeeItemId, shopeeModelId).
- Returns `201 Created` with `ShopeeProductLinkResponse` via `ToCreatedResult()`.
- Same tenant-scoped authorization policy as the existing link endpoint.

### Error handling

All failures flow through the existing `Result`/`Error` → HTTP mapping: validation 400; `ConnectionNotFound` / `ItemNotFoundInShop` / `ModelMismatch` 404; `AlreadyLinked` / `BarcodeAlreadyExists` / `SkuAlreadyExists` 409; Shopee fetch/auth failures use the existing `ItemFetchFailed` / `AuthFailed` errors. A fresh resolve against Shopee is required — if Shopee is unreachable, the whole operation fails; nothing is created from stale data.

## Frontend Design (partner repo)

`src/features/partner/shopee-products/`

- **Entry point:** each unlinked sellable-unit row gains a "Create product" action alongside the existing "Link" action. `LinkProductDrawer` is unchanged.
- **New component** `components/create-product-drawer.tsx` (Mantine `Drawer` `position="right"`, Mantine Form + Zod via `zodResolver`):
  - Read-only header: Shopee item/model name and SKU (same presentation as the link drawer).
  - Pre-filled editable fields: Name (model name ?? item name), SKU (model SKU ?? item SKU), Stock quantity (unit's Shopee stock).
  - Empty required fields: Barcode, Cost.
  - Optional field: Low-stock threshold.
- **Service:** `createShopeeLinkedProduct(tenantId, payload)` in `service.ts` calling the new endpoint.
- **On success:** green notification, invalidate `shopeeItemsQueryOptions` and `shopeeProductLinksQueryOptions` queries (same pattern as the link drawer), reset and close.
- **On error:** `<Alert role="alert">` with `getApiErrorDetail` (surfaces duplicate barcode, already linked, etc.).
- **i18n:** all strings via react-intl under `partner.shopeeProducts.create.*`; run `pnpm extract` after.

## Testing

- **Wrapsfer.Application.Tests** (xUnit + Moq + FluentAssertions):
  - Handler: happy path (product and link both added, one save, low-stock check applied); each failure branch (already linked, no connection, resolver failure, duplicate barcode, duplicate SKU) creates nothing; partner-entered values used for the product while the link snapshot uses resolver values.
  - Validator: required/boundary rules for the new command.
- **Wrapsfer.Domain.Tests:** no new entity logic — nothing new needed.
- **Frontend:** Vitest test for `createShopeeLinkedProduct` in `service.test.ts`, mirroring existing service tests.

## Out of Scope

- Bulk create from multiple Shopee items (API shape does not preclude adding it later).
- Pulling Shopee price into product cost (Shopee price ≠ Wrapsfer cost; partner enters cost).
- Image import from Shopee.
- Any change to stock-sync direction or cadence.
