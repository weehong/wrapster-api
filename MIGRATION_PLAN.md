# Migration Plan: JavaScript Wrapsfer to C# wrapsfer-api

## Context

The JavaScript Wrapsfer application (React + Appwrite BaaS) is a packaging tracking system for warehouse/logistics teams. It needs to be migrated to the existing C# ASP.NET Core Clean Architecture API (`wrapsfer-api`), which already has solid infrastructure: Keycloak multi-tenant auth, EF Core + PostgreSQL, MediatR CQRS, audit logging interceptors, and Serilog.

The migration replaces:
- Appwrite collections -> PostgreSQL tables via EF Core
- Appwrite Functions -> API controller endpoints
- Trigger.dev -> RabbitMQ consumers (with transactional outbox for durability)
- Appwrite Storage -> File storage abstraction (local disk)
- Resend (JS) -> Resend (C# -- same provider)

Each phase is a complete vertical slice for one module: domain entity, errors, repository, EF config, migration, commands/queries, controller, and Bruno tests.

---

## Phase 0: Shared Infrastructure

**Goal:** Establish shared abstractions and base classes needed by all subsequent modules.

### 0.1 Application Abstractions

| File | Purpose |
|------|---------|
| `src/Wrapsfer.Application/Abstractions/Messaging/IQuery.cs` | `IQuery<TResponse> : IRequest<Result<TResponse>>` |
| `src/Wrapsfer.Application/Abstractions/Messaging/IQueryHandler.cs` | `IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>` |
| `src/Wrapsfer.Application/Common/PagedResult.cs` | Generic `PagedResult<T>` with `Items`, `TotalCount`, `Page`, `PageSize` |

### 0.2 API Base Controller

| File | Purpose |
|------|---------|
| `src/Wrapsfer.Api/Controllers/ApiControllerBase.cs` | Maps `Result<T>` to HTTP responses (Ok/NotFound/BadRequest/Conflict). All feature controllers inherit from this. |

### Verification
- `make build` passes

---

## Phase 1: Products Module

**Goal:** Full vertical slice for product management -- domain through API.

### 1.1 Domain

**Enums:**
- `src/Wrapsfer.Domain/Enums/ProductType.cs` -- `Single`, `Bundle`

**Entities:**
- `src/Wrapsfer.Domain/Entities/Product.cs` -- extends `AuditableEntity`
  - Properties: `TenantId`, `Barcode` (unique per tenant), `SkuCode?`, `Name`, `Type` (ProductType), `Cost` (decimal), `StockQuantity` (int)
  - Domain methods: `DeductStock(amount)` returns `Result`, `RestoreStock(amount)`
  - Factory: `Create(...)` returns `Result<Product>`
- `src/Wrapsfer.Domain/Entities/ProductComponent.cs` -- extends `AuditableEntity`
  - Properties: `TenantId`, `ParentProductId`, `ChildProductId`, `Quantity`
  - Navigation props to `Product`
  - Unique constraint on `(TenantId, ParentProductId, ChildProductId)`

**Errors:**
- `src/Wrapsfer.Domain/Errors/ProductErrors.cs` -- `NotFound`, `BarcodeAlreadyExists`, `SkuAlreadyExists`, `InsufficientStock`, `CannotDeductBundleStock`

**Repositories:**
- `src/Wrapsfer.Domain/Repositories/IProductRepository.cs` -- GetById, GetByBarcode, GetBySkuCode, GetByBarcodes (batch), List (search/filter/paginate), Add, Remove
- `src/Wrapsfer.Domain/Repositories/IProductComponentRepository.cs` -- GetByParentId, GetByChildId, Add, Remove

### 1.2 Infrastructure

**EF Configurations:**
- `ProductConfiguration.cs` -- Table `"Products"`, unique index on `(TenantId, Barcode)`, index on `(TenantId, SkuCode)`, `ProductType` as string, `Cost` as `decimal(18,2)`
- `ProductComponentConfiguration.cs` -- Table `"ProductComponents"`, unique index on `(TenantId, ParentProductId, ChildProductId)`, FK with `DeleteBehavior.Restrict`

**Repositories:**
- `ProductRepository.cs`, `ProductComponentRepository.cs` -- all queries filter by TenantId

**Wiring:**
- Add `DbSet<Product>`, `DbSet<ProductComponent>` to `ApplicationDbContext`
- Register repositories in `DependencyInjection.cs`
- Migration: `dotnet ef migrations add AddProducts`

### 1.3 Application

**Commands** (each with Command, Validator, Handler) in `src/Wrapsfer.Application/Products/`:
- `CreateProduct` -- `ICommand<Guid>`: barcode uniqueness check, factory creation, returns Id
- `UpdateProduct` -- `ICommand`: partial update of name/sku/cost/stock/type
- `DeleteProduct` -- `ICommand`: also removes related ProductComponents
- `UpdateProductStock` -- `ICommand`: explicit stock quantity set

**Queries** (each with Query, Handler):
- `GetProductById` -- `IQuery<ProductResponse>`
- `GetProductByBarcode` -- `IQuery<ProductResponse?>`
- `GetProductBySku` -- `IQuery<ProductResponse?>`
- `ListProducts` -- `IQuery<PagedResult<ProductResponse>>` with search, type filter, pagination
- `GetProductWithComponents` -- `IQuery<ProductWithComponentsResponse>`

**Product Component Commands** in `src/Wrapsfer.Application/ProductComponents/`:
- `AddProductComponent` -- `ICommand<Guid>`: validates parent is bundle, child is single
- `UpdateProductComponentQuantity` -- `ICommand`
- `RemoveProductComponent` -- `ICommand`
- `RemoveAllProductComponents` -- `ICommand`: for a parent product

### 1.4 API

**`ProductsController`** (`src/Wrapsfer.Api/Controllers/V1/ProductsController.cs`):
- `POST /api/v1/products`
- `GET /api/v1/products` (list with query params)
- `GET /api/v1/products/{id}`
- `GET /api/v1/products/by-barcode/{barcode}`
- `GET /api/v1/products/by-sku/{skuCode}`
- `PUT /api/v1/products/{id}`
- `DELETE /api/v1/products/{id}`
- `PATCH /api/v1/products/{id}/stock`
- `GET /api/v1/products/{id}/components`
- `POST /api/v1/products/{id}/components`
- `PUT /api/v1/products/{id}/components/{componentId}`
- `DELETE /api/v1/products/{id}/components/{componentId}`
- `DELETE /api/v1/products/{id}/components` (remove all)

### Verification
- Full CRUD via Bruno requests
- Barcode uniqueness enforced per tenant
- Audit logs auto-created via existing interceptors
- `make build` and `make test` pass

---

## Phase 2: Packaging Module

**Goal:** Full vertical slice for packaging records, items, stock deduction/restoration, and cache-aside pattern.

### 2.1 Domain

**Entities:**
- `src/Wrapsfer.Domain/Entities/PackagingRecord.cs` -- extends `AuditableEntity`
  - Properties: `TenantId`, `PackagingDate` (DateOnly), `WaybillNumber`
  - Navigation: `ICollection<PackagingItem> Items`
  - Unique on `(TenantId, PackagingDate, WaybillNumber)`
- `src/Wrapsfer.Domain/Entities/PackagingItem.cs` -- extends `AuditableEntity`
  - Properties: `TenantId`, `PackagingRecordId` (FK), `ProductBarcode`, `ScannedAt` (DateTime)
  - FK cascade delete from PackagingRecord
- `src/Wrapsfer.Domain/Entities/PackagingCache.cs` -- extends `BaseEntity` (not auditable)
  - Properties: `TenantId`, `CacheDate` (DateOnly), `Data` (JSON), `CachedAtUtc`
  - Unique on `(TenantId, CacheDate)`

**Errors:**
- `src/Wrapsfer.Domain/Errors/PackagingErrors.cs` -- `NotFound`, `DuplicateWaybillForDate`, `ItemNotFound`

**Repositories:**
- `IPackagingRecordRepository` -- GetByIdWithItems, GetByDateAndWaybill, ListByDateWithItems, Add, Remove
- `IPackagingCacheRepository` -- GetByDate, Add, Remove

### 2.2 Infrastructure

**EF Configurations:**
- `PackagingRecordConfiguration.cs` -- unique index on `(TenantId, PackagingDate, WaybillNumber)`, index on `(TenantId, PackagingDate)`
- `PackagingItemConfiguration.cs` -- FK to PackagingRecord (cascade), index on `(TenantId, PackagingRecordId)`, index on `ProductBarcode`
- `PackagingCacheConfiguration.cs` -- unique index on `(TenantId, CacheDate)`, `Data` as `jsonb`

**Repositories:**
- `PackagingRecordRepository.cs`, `PackagingCacheRepository.cs`

**Services:**
- `src/Wrapsfer.Application/Abstractions/IPackagingCacheService.cs` -- Get, Set, Invalidate
- `src/Wrapsfer.Infrastructure/Services/PackagingCacheService.cs` -- uses `IPackagingCacheRepository`, JSON serialization

**Wiring:**
- Add DbSets to `ApplicationDbContext`
- Register repositories and `IPackagingCacheService` in `DependencyInjection.cs`
- Migration: `dotnet ef migrations add AddPackaging`

### 2.3 Application

**Stock Service** (`src/Wrapsfer.Application/Services/StockService.cs`, registered as scoped):
- `CalculateStockRequirements(items, components)` -- maps product IDs to required deduction amounts (singles=1, bundles=sum of component quantities)
- `ValidateStock(requirements)` -- returns `Result` with insufficient stock details
- `DeductStock(requirements)` -- calls `Product.DeductStock()` on each (within same EF transaction)
- `RestoreStock(requirements)` -- calls `Product.RestoreStock()` on each

Key advantage over JS: all stock operations happen within a **single EF Core transaction** via `SaveChangesAsync`. No manual rollback needed.

**Commands** in `src/Wrapsfer.Application/Packaging/`:
- `CreatePackagingRecord` -- `ICommand<Guid>`: duplicate check (date+waybill), create record+items, validate & deduct stock, all in one SaveChanges
- `UpdatePackagingRecord` -- `ICommand`: update waybill and/or replace items, recalculate stock diffs, invalidate cache
- `DeletePackagingRecord` -- `ICommand`: delete record+items, restore stock, invalidate cache

**Queries:**
- `GetPackagingByDate` -- `IQuery<List<PackagingRecordWithProductsResponse>>`: cache-aside (today always fresh, historical cached), enriches items with product names and bundle info
- `GetPackagingRecordById` -- `IQuery<PackagingRecordResponse>`
- `CheckDuplicateWaybill` -- `IQuery<bool>`

### 2.4 API

**`PackagingController`** (`src/Wrapsfer.Api/Controllers/V1/PackagingController.cs`):
- `POST /api/v1/packaging`
- `PUT /api/v1/packaging/{id}`
- `DELETE /api/v1/packaging/{id}`
- `GET /api/v1/packaging/by-date/{date}`
- `GET /api/v1/packaging/{id}`
- `GET /api/v1/packaging/check-duplicate` (query params: date, waybill)

### Verification
- Create packaging record with items and verify stock deducted
- Delete record and verify stock restored
- Bundle stock deduction deducts component products
- Cache hit on second request for historical date
- `make build` and `make test` pass

---

## Phase 3: File Storage Module

**Goal:** File storage abstraction and StoredFile entity -- prerequisite for Jobs module.

### 3.1 Domain

**Entity:**
- `src/Wrapsfer.Domain/Entities/StoredFile.cs` -- extends `AuditableEntity`
  - Properties: `TenantId`, `FileName`, `ContentType`, `SizeBytes` (long), `StoragePath`

**Repository:**
- `IStoredFileRepository` -- GetById, Add, Remove

### 3.2 Infrastructure

**EF Configuration:**
- `StoredFileConfiguration.cs` -- index on `TenantId`

**Repository:**
- `StoredFileRepository.cs`

**Storage Service:**
- `src/Wrapsfer.Application/Abstractions/IFileStorageService.cs` -- Upload (returns Guid), Download (returns Stream), Delete, GetMetadata
- `src/Wrapsfer.Infrastructure/Storage/LocalFileStorageService.cs` -- stores on disk at `./storage/{tenantId}/{year}/{month}/{fileId}/{fileName}`
- `src/Wrapsfer.Infrastructure/Storage/StorageOptions.cs` -- configurable base path

**Wiring:**
- Add `DbSet<StoredFile>` to `ApplicationDbContext`
- Register `IStoredFileRepository` and `IFileStorageService` in `DependencyInjection.cs`
- Migration: `dotnet ef migrations add AddStoredFiles`

### 3.3 API

**`FilesController`** (`src/Wrapsfer.Api/Controllers/V1/FilesController.cs`):
- `POST /api/v1/files` (upload, multipart form)
- `GET /api/v1/files/{id}` (download, returns FileStreamResult)
- `DELETE /api/v1/files/{id}`

### Verification
- File upload/download round-trips correctly
- File metadata persisted in database
- `make build` and `make test` pass

---

## Phase 4: Jobs Module

**Goal:** RabbitMQ job infrastructure + ImportJob entity + all five job types + Resend email + Jobs API.

### 4.1 Domain

**Enums:**
- `src/Wrapsfer.Domain/Enums/JobAction.cs` -- `ImportExcel`, `ExportExcel`, `ExportReportingExcel`, `ExportReportingPdf`, `SendReportEmail`
- `src/Wrapsfer.Domain/Enums/JobStatus.cs` -- `Pending`, `Processing`, `Completed`, `Failed`

**Entity:**
- `src/Wrapsfer.Domain/Entities/ImportJob.cs` -- extends `AuditableEntity`
  - Properties: `TenantId`, `UserId`, `Action` (JobAction), `Status` (JobStatus), `FileId?` (Guid), `ResultFileId?` (Guid), `Filters?` (JSON), `Stats?` (JSON), `Error?`, `CreatedAtUtc`, `CompletedAtUtc?`
  - Methods: `MarkProcessing()`, `MarkCompleted(resultFileId?, stats?)`, `MarkFailed(error)`

**Errors:**
- `src/Wrapsfer.Domain/Errors/JobErrors.cs` -- `NotFound`, `InvalidAction`, `AlreadyProcessing`, `FileNotFound`

**Repository:**
- `IImportJobRepository` -- GetById, ListByUser, GetActiveJobs, Add, Remove

### 4.2 Infrastructure

**EF Configuration:**
- `ImportJobConfiguration.cs` -- `Filters`/`Stats` as `jsonb`, `JobAction`/`JobStatus` as string, index on `(TenantId, UserId, Status)`, index on `(TenantId, UserId, CreatedAtUtc)`

**Repository:**
- `ImportJobRepository.cs`

**RabbitMQ Setup:**

The job system uses a **transactional outbox pattern** to guarantee no jobs are lost, even if RabbitMQ is down:

1. **Producer side (API request):**
   - Create `ImportJob` entity (status=Pending) and save to database
   - Publish message to RabbitMQ with the job Id
   - If RabbitMQ is unavailable at publish time, the job record still exists in the DB. A background `OutboxProcessor` (hosted service) periodically scans for Pending jobs older than a threshold and re-publishes them.

2. **Consumer side (background worker):**
   - Consumer receives message, marks job as Processing, does the work, marks Completed/Failed
   - Uses **manual acknowledgement** -- message is only acked after the job completes or permanently fails
   - If the consumer crashes mid-processing, RabbitMQ redelivers the message
   - Consumers are idempotent -- re-processing a job that's already Completed is a no-op

3. **RabbitMQ configuration:**
   - **Durable queues** -- survive broker restarts
   - **Persistent messages** -- written to disk before ack
   - **Dead-letter exchange** -- failed messages (after max retries) routed to DLQ for inspection
   - **Prefetch count = 1** -- one job at a time per consumer for predictable resource usage

**Queue Files:**
- `src/Wrapsfer.Application/Abstractions/IJobQueue.cs` -- `PublishAsync(Guid jobId, JobAction action, CancellationToken ct)`
- `src/Wrapsfer.Infrastructure/Queue/RabbitMqJobQueue.cs` -- publishes messages to RabbitMQ exchanges
- `src/Wrapsfer.Infrastructure/Queue/RabbitMqOptions.cs` -- `HostName`, `Port`, `UserName`, `Password`, `VirtualHost` from appsettings
- `src/Wrapsfer.Infrastructure/Queue/JobConsumerHostedService.cs` -- `IHostedService` that subscribes to queues and dispatches to job handlers
- `src/Wrapsfer.Infrastructure/Queue/OutboxProcessorHostedService.cs` -- periodic scan for orphaned Pending jobs, re-publishes to RabbitMQ

**Job Implementations** (`src/Wrapsfer.Infrastructure/Jobs/`):

| Job | Replaces | Logic |
|-----|----------|-------|
| `ProductImportJob` | `trigger/product-import.ts` | Download Excel, two-pass import (singles then bundles), upsert by barcode, update stats |
| `ProductExportJob` | `trigger/product-export.ts` | Query all products, generate Excel with ClosedXML, upload via IFileStorageService |
| `ReportExportJob` | `trigger/report-export.ts` | Query packaging records in date range, calculate summaries, generate Excel (4 sheets) or PDF (4 pages via QuestPDF) |
| `SendReportEmailJob` | `trigger/send-report-email.ts` | Download file, send via IEmailService with attachment |

**Email (Resend):**
- `src/Wrapsfer.Application/Abstractions/IEmailService.cs` -- `SendAsync(to, subject, htmlBody, attachments?)`
- `src/Wrapsfer.Infrastructure/Email/ResendEmailService.cs` -- Resend HTTP API implementation
- `src/Wrapsfer.Infrastructure/Email/ResendOptions.cs` -- `ApiKey`, `FromAddress` from appsettings

**NuGet Packages:**
- `RabbitMQ.Client` -- RabbitMQ .NET client
- `ClosedXML` -- Excel generation/parsing
- `QuestPDF` -- PDF generation

**Docker Compose:**
- Add RabbitMQ 4 service (port 5672 for AMQP, 15672 for management UI)
- Health check on RabbitMQ before API starts

**Wiring:**
- Add `DbSet<ImportJob>` to `ApplicationDbContext`
- Register repository, `IJobQueue`, `IEmailService`, `JobConsumerHostedService`, `OutboxProcessorHostedService` in `DependencyInjection.cs`
- Migration: `dotnet ef migrations add AddImportJobs`

### 4.3 Application

**Commands** in `src/Wrapsfer.Application/Jobs/`:
- `QueueImportJob` -- `ICommand<Guid>` with FileId
- `QueueExportJob` -- `ICommand<Guid>` with optional type filter
- `QueueReportExportJob` -- `ICommand<Guid>` with StartDate, EndDate, Format
- `QueueSendReportEmailJob` -- `ICommand<Guid>` with FileId, Recipients, DateRange
- `DeleteJob` -- `ICommand` with Id (deletes job + associated files)

**Queries:**
- `GetJobById` -- `IQuery<JobResponse>`
- `ListUserJobs` -- `IQuery<PagedResult<JobResponse>>` with action/status filters
- `GetActiveJobs` -- `IQuery<List<JobResponse>>`

### 4.4 API

**`JobsController`** (`src/Wrapsfer.Api/Controllers/V1/JobsController.cs`):
- `POST /api/v1/jobs/import` (multipart form)
- `POST /api/v1/jobs/export`
- `POST /api/v1/jobs/report-export`
- `POST /api/v1/jobs/send-report-email`
- `GET /api/v1/jobs/{id}`
- `GET /api/v1/jobs` (list)
- `GET /api/v1/jobs/active`
- `DELETE /api/v1/jobs/{id}`

### Verification
- Queue import job via API, poll until completed, verify products created
- Queue export, download resulting file
- Generate packaging report (Excel and PDF)
- Send email via Resend
- Verify outbox processor recovers jobs when RabbitMQ is restarted
- Full job lifecycle end-to-end
- `make build` and `make test` pass

---

## Phase 5: Testing and Polish

### 5.1 Unit Tests

- **Domain**: Product stock deduction/restoration, factory methods, job status transitions
- **Application**: Command/query handlers with mocked repositories (focus on CreatePackagingRecord stock logic)

### 5.2 Integration Tests

- Use `WebApplicationFactory<Program>` with test database
- Product CRUD cycle, packaging with stock deduction, multi-tenant isolation

### 5.3 Bruno API Collection

- Update `bruno/` directory with requests for all new endpoints
- Token acquisition, product CRUD, packaging workflow, job management

---

## Key Architectural Decisions

1. **EF Core transactions replace manual rollback** -- Stock deduction + record creation happen in a single `SaveChangesAsync`, eliminating the elaborate rollback logic from the JS version.

2. **Automatic audit logging** -- The existing `AuditLogInterceptor` captures all entity changes. No manual audit log creation needed (unlike JS app's per-operation logging).

3. **TenantId on every entity** -- All repository queries filter by TenantId from `ITenantContext`. Data isolation is enforced at the repository layer.

4. **DateOnly for packaging dates** -- Maps to PostgreSQL `date` type. Better than the JS string approach.

5. **PackagingCache in DB** -- Mirrors the JS approach. Can migrate to Redis later if needed.

6. **RabbitMQ with transactional outbox** -- Durable queues + persistent messages + manual ack guarantee no jobs are lost. The outbox processor catches any messages that fail to publish (e.g., RabbitMQ down) by scanning Pending jobs in the database and re-publishing them. Consumers are idempotent so redelivery is safe.

7. **Resend for email** -- Same provider as the JS app. HTTP API-based, no SMTP server to manage.

## Phase Dependency Graph

```
Phase 0 (Shared Infrastructure)
  |
  v
Phase 1 (Products Module)
  |
  v
Phase 2 (Packaging Module) -- depends on Products for stock deduction
  |
  v
Phase 3 (File Storage Module)
  |
  v
Phase 4 (Jobs Module) -- depends on Products, Packaging, and File Storage
  |
  v
Phase 5 (Testing + Polish)
```

## Critical Files to Modify (across all phases)

- `src/Wrapsfer.Infrastructure/Persistence/ApplicationDbContext.cs` -- add DbSets per phase
- `src/Wrapsfer.Infrastructure/DependencyInjection.cs` -- register services per phase
- `Directory.Packages.props` -- add RabbitMQ.Client, ClosedXML, QuestPDF in Phase 4
- `src/Wrapsfer.Infrastructure/Wrapsfer.Infrastructure.csproj` -- add package references in Phase 4
- `compose.yml` -- add RabbitMQ service in Phase 4

## Reusable Existing Patterns

- `src/Wrapsfer.Domain/Common/AuditableEntity.cs` -- base for all new entities
- `src/Wrapsfer.Application/Abstractions/Messaging/ICommand.cs` -- template for IQuery
- `src/Wrapsfer.Infrastructure/Persistence/Configurations/AuditLogConfiguration.cs` -- template for all EF configs
- `src/Wrapsfer.Api/Controllers/V1/MeController.cs` -- controller attributes/routing pattern
- `src/Wrapsfer.Infrastructure/Persistence/Interceptors/AuditLogInterceptor.cs` -- already handles audit for all entities
- `src/Wrapsfer.Application/Behaviors/ValidationBehavior.cs` -- auto-validates all commands with FluentValidation
