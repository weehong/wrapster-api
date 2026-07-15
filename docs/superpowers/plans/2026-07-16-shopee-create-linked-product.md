# Create Wrapsfer Product from Shopee Item — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a partner create a new Wrapsfer product pre-filled from a Shopee item and link it to that item atomically, from the Shopee products page.

**Architecture:** One new CQRS command (`CreateShopeeLinkedProductCommand`) in the api repo's Shopee module creates the `Product` and the `ShopeeProductLink` in a single `SaveChangesAsync` transaction, reusing `ShopeeSellableUnitResolver` and the existing entity factories. The partner repo gains a "Create product" row action opening a pre-filled drawer form that calls one new endpoint. Spec: `docs/superpowers/specs/2026-07-16-shopee-create-linked-product-design.md` (api repo).

**Tech Stack:** .NET 10 / MediatR / FluentValidation / EF Core / xUnit + Moq + FluentAssertions (api). React 19 / TanStack Start + Query / Mantine v9 + mantine-form-zod-resolver / Zod v4 / react-intl / Vitest (partner).

## Global Constraints

- Two separate git repos: backend work in `/home/vernon/Projects/Wrapsfer/api` (branch `shopee`), frontend work in `/home/vernon/Projects/Wrapsfer/partner`. Commit in the repo the task touches.
- api: **Never use `var`** — explicit types always (pre-commit hook rejects `var`). One type per file. No consecutive blank lines. `TreatWarningsAsErrors` is on.
- api: every repository call is tenant-scoped; all failures return `Result`/`Result<T>` with error constants from `Errors` classes — no inline `new Error(...)`.
- api: MediatR handlers and FluentValidation validators are auto-registered by assembly scan in `Application/DependencyInjection.cs` — no DI registration needed for new commands.
- Product Type is always `Single` for this flow (spec decision).
- The handler performs local DB checks (link exists, barcode, SKU) **before** the remote Shopee resolve call — same fail-fast philosophy the link handler's tests assert ("fails before fetching Shopee").
- partner: all files kebab-case, one component per file, no cross-feature imports, all user-visible strings through react-intl (`partner.shopeeProducts.create.*`), run `pnpm extract` after adding messages.
- partner: forms use Mantine `useForm` + `zodResolver` from `mantine-form-zod-resolver`; create/edit flows use right-side `Drawer`; async errors via `<Alert role="alert">` with `getApiErrorDetail`.

---

### Task 1: Backend — command + validator (api repo)

**Files:**
- Create: `src/Wrapsfer.Application/Shopee/Commands/CreateShopeeLinkedProduct/CreateShopeeLinkedProductCommand.cs`
- Create: `src/Wrapsfer.Application/Shopee/Commands/CreateShopeeLinkedProduct/CreateShopeeLinkedProductCommandValidator.cs`
- Test: `tests/Wrapsfer.Application.Tests/Shopee/Validators/CreateShopeeLinkedProductCommandValidatorTests.cs`

**Interfaces:**
- Consumes: `ICommand<T>` from `Wrapsfer.Application.Abstractions.Messaging`; `ShopeeProductLinkResponse` from `Wrapsfer.Application.Shopee.Responses` (exists).
- Produces: `CreateShopeeLinkedProductCommand(string TenantId, long ShopeeItemId, long ShopeeModelId, string Barcode, string Name, string? SkuCode, decimal Cost, int StockQuantity, int? LowStockThreshold) : ICommand<ShopeeProductLinkResponse>` — Tasks 2 and 3 construct exactly this record with this parameter order.

All commands in this task run from `/home/vernon/Projects/Wrapsfer/api`.

- [ ] **Step 1: Write the failing validator test**

Note: `Xunit`, `FluentAssertions`, `Moq`, `Microsoft.Extensions.Options` are global usings in the test project — do not add `using` lines for them.

Create `tests/Wrapsfer.Application.Tests/Shopee/Validators/CreateShopeeLinkedProductCommandValidatorTests.cs`:

```csharp
using FluentValidation.Results;
using Wrapsfer.Application.Shopee.Commands.CreateShopeeLinkedProduct;

namespace Wrapsfer.Application.Tests.Shopee.Validators;

public class CreateShopeeLinkedProductCommandValidatorTests
{
    private readonly CreateShopeeLinkedProductCommandValidator _validator = new();

    private static CreateShopeeLinkedProductCommand CreateCommand(
        string tenantId = "test-tenant",
        long shopeeItemId = 1001,
        long shopeeModelId = 0,
        string barcode = "BC-001",
        string name = "Widget",
        string? skuCode = "SKU-1",
        decimal cost = 9.99m,
        int stockQuantity = 50,
        int? lowStockThreshold = 10) =>
        new(tenantId, shopeeItemId, shopeeModelId, barcode, name, skuCode, cost, stockQuantity,
            lowStockThreshold);

    [Fact]
    public void Validate_WhenValid_HasNoErrors()
    {
        ValidationResult result = _validator.Validate(CreateCommand());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenTenantIdEmpty_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(tenantId: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TenantId");
    }

    [Fact]
    public void Validate_WhenShopeeItemIdNotPositive_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(shopeeItemId: 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ShopeeItemId");
    }

    [Fact]
    public void Validate_WhenShopeeModelIdNegative_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(shopeeModelId: -1));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ShopeeModelId");
    }

    [Fact]
    public void Validate_WhenBarcodeEmpty_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(barcode: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Barcode");
    }

    [Fact]
    public void Validate_WhenNameEmpty_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(name: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_WhenCostNegative_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(cost: -1m));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Cost");
    }

    [Fact]
    public void Validate_WhenStockQuantityNegative_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(stockQuantity: -1));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "StockQuantity");
    }

    [Fact]
    public void Validate_WhenLowStockThresholdNegative_HasValidationError()
    {
        ValidationResult result = _validator.Validate(CreateCommand(lowStockThreshold: -1));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LowStockThreshold");
    }

    [Fact]
    public void Validate_WhenLowStockThresholdNull_HasNoErrors()
    {
        ValidationResult result = _validator.Validate(CreateCommand(lowStockThreshold: null));

        result.IsValid.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet build 2>&1 | tail -5`
Expected: build FAILS — `CreateShopeeLinkedProductCommand` and `CreateShopeeLinkedProductCommandValidator` do not exist. (A compile failure is this step's expected "red".)

- [ ] **Step 3: Write the command and validator**

Create `src/Wrapsfer.Application/Shopee/Commands/CreateShopeeLinkedProduct/CreateShopeeLinkedProductCommand.cs`:

```csharp
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Responses;

namespace Wrapsfer.Application.Shopee.Commands.CreateShopeeLinkedProduct;

public sealed record CreateShopeeLinkedProductCommand(
    string TenantId,
    long ShopeeItemId,
    long ShopeeModelId,
    string Barcode,
    string Name,
    string? SkuCode,
    decimal Cost,
    int StockQuantity,
    int? LowStockThreshold) : ICommand<ShopeeProductLinkResponse>;
```

Create `src/Wrapsfer.Application/Shopee/Commands/CreateShopeeLinkedProduct/CreateShopeeLinkedProductCommandValidator.cs`:

```csharp
using FluentValidation;

namespace Wrapsfer.Application.Shopee.Commands.CreateShopeeLinkedProduct;

public sealed class CreateShopeeLinkedProductCommandValidator
    : AbstractValidator<CreateShopeeLinkedProductCommand>
{
    public CreateShopeeLinkedProductCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ShopeeItemId).GreaterThan(0);
        RuleFor(x => x.ShopeeModelId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Barcode).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0).When(x => x.LowStockThreshold.HasValue);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build 2>&1 | tail -3 && dotnet test --no-restore --no-build --filter "FullyQualifiedName~CreateShopeeLinkedProductCommandValidatorTests" 2>&1 | tail -5`
Expected: build succeeds; 10 tests PASS.

- [ ] **Step 5: Format and commit**

```bash
make format-all
git add src/Wrapsfer.Application/Shopee/Commands/CreateShopeeLinkedProduct tests/Wrapsfer.Application.Tests/Shopee/Validators/CreateShopeeLinkedProductCommandValidatorTests.cs
git commit -m "feat: add CreateShopeeLinkedProduct command and validator"
```

---

### Task 2: Backend — handler (api repo)

**Files:**
- Create: `src/Wrapsfer.Application/Shopee/Commands/CreateShopeeLinkedProduct/CreateShopeeLinkedProductCommandHandler.cs`
- Test: `tests/Wrapsfer.Application.Tests/Shopee/Commands/CreateShopeeLinkedProductCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `CreateShopeeLinkedProductCommand` (Task 1); existing `IProductRepository`, `IShopeeProductLinkRepository`, `IShopeeShopConnectionRepository`, `ITenantSettingsRepository`, `ShopeeSellableUnitResolver`, `ITenantContext`, `IUnitOfWork`, `IOptions<ProductSettings>`, `Product.Create`, `ShopeeProductLink.Create`, `ShopeeProductLinkResponseMapper.Map(link, product)`.
- Produces: `CreateShopeeLinkedProductCommandHandler : ICommandHandler<CreateShopeeLinkedProductCommand, ShopeeProductLinkResponse>` — MediatR dispatches to it automatically; Task 3's endpoint just sends the command.

All commands in this task run from `/home/vernon/Projects/Wrapsfer/api`.

- [ ] **Step 1: Write the failing handler tests**

Create `tests/Wrapsfer.Application.Tests/Shopee/Commands/CreateShopeeLinkedProductCommandHandlerTests.cs`:

```csharp
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Products;
using Wrapsfer.Application.Shopee.Commands.CreateShopeeLinkedProduct;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Events;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee.Commands;

public class CreateShopeeLinkedProductCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IShopeeProductLinkRepository> _linkRepository = new();
    private readonly Mock<IShopeeShopConnectionRepository> _connectionRepository = new();
    private readonly Mock<IShopeeGateway> _gateway = new();
    private readonly Mock<ITenantSettingsRepository> _tenantSettingsRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly CreateShopeeLinkedProductCommandHandler _handler;

    public CreateShopeeLinkedProductCommandHandlerTests()
    {
        _tenantContext.Setup(c => c.Username).Returns("admin@acme");
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.TenantSettings?)null);
        IOptions<ProductSettings> settings = Options.Create(new ProductSettings { GlobalLowStockThreshold = 10 });
        _handler = new CreateShopeeLinkedProductCommandHandler(
            _productRepository.Object,
            _linkRepository.Object,
            _connectionRepository.Object,
            new ShopeeSellableUnitResolver(_gateway.Object),
            _tenantSettingsRepository.Object,
            _tenantContext.Object,
            _unitOfWork.Object,
            settings);
    }

    [Fact]
    public async Task Handle_WhenValid_CreatesProductAndLinkAtomically()
    {
        ShopeeShopConnection connection = CreateConnection();
        SetupConnection(connection);
        SetupModellessItem(connection, stockQuantity: 9);
        Product? addedProduct = null;
        ShopeeProductLink? addedLink = null;
        _productRepository.Setup(r => r.Add(It.IsAny<Product>()))
            .Callback<Product>(p => addedProduct = p);
        _linkRepository.Setup(r => r.Add(It.IsAny<ShopeeProductLink>()))
            .Callback<ShopeeProductLink>(l => addedLink = l);

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            CreateCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        addedProduct.Should().NotBeNull();
        addedProduct!.Name.Should().Be("Local Widget");
        addedProduct.Barcode.Should().Be("BC-100");
        addedProduct.SkuCode.Should().Be("LOCAL-SKU");
        addedProduct.StockQuantity.Should().Be(9);
        addedProduct.Type.Should().Be(ProductType.Single);
        addedLink.Should().NotBeNull();
        addedLink!.ProductId.Should().Be(addedProduct.Id);
        addedLink.ShopeeItemId.Should().Be(1001);
        addedLink.ShopeeItemName.Should().Be("Shopee Item");
        addedLink.ShopeeItemSku.Should().Be("SHOPEE-SKU");
        addedLink.LinkedBy.Should().Be("admin@acme");
        result.Value.ProductName.Should().Be("Local Widget");
        result.Value.ShopeeItemName.Should().Be("Shopee Item");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenModelItem_UsesModelSnapshotForLink()
    {
        ShopeeShopConnection connection = CreateConnection();
        SetupConnection(connection);
        _gateway.Setup(g => g.GetItemBaseInfoAsync(
                connection.ShopId,
                connection.AccessToken,
                It.IsAny<IReadOnlyCollection<long>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ShopeeItemDetail>>([
                new ShopeeItemDetail(1001, "Shopee Item", "ITEM-SKU", "NORMAL", true, null, null)
            ]));
        _gateway.Setup(g => g.GetModelListAsync(
                connection.ShopId,
                connection.AccessToken,
                1001,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ShopeeItemModel>>([
                new ShopeeItemModel(2002, "Large", "MODEL-SKU", 5)
            ]));
        ShopeeProductLink? addedLink = null;
        _linkRepository.Setup(r => r.Add(It.IsAny<ShopeeProductLink>()))
            .Callback<ShopeeProductLink>(l => addedLink = l);

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            CreateCommand(modelId: 2002), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        addedLink.Should().NotBeNull();
        addedLink!.ShopeeModelId.Should().Be(2002);
        addedLink.ShopeeModelName.Should().Be("Large");
        addedLink.ShopeeItemSku.Should().Be("MODEL-SKU");
    }

    [Fact]
    public async Task Handle_WhenStockBelowThreshold_RaisesLowStockEvent()
    {
        ShopeeShopConnection connection = CreateConnection();
        SetupConnection(connection);
        SetupModellessItem(connection, stockQuantity: 2);
        Product? addedProduct = null;
        _productRepository.Setup(r => r.Add(It.IsAny<Product>()))
            .Callback<Product>(p => addedProduct = p);

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            CreateCommand(stockQuantity: 2, lowStockThreshold: 5), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        addedProduct.Should().NotBeNull();
        addedProduct!.DomainEvents.OfType<LowStockDetectedEvent>().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WhenUnitAlreadyLinked_FailsBeforeFetchingShopee()
    {
        _linkRepository.Setup(r => r.ExistsAsync(TenantId, 1001, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            CreateCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeProductLinkErrors.AlreadyLinked);
        _gateway.Verify(g => g.GetItemBaseInfoAsync(
            It.IsAny<long>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<long>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        VerifyNothingCreated();
    }

    [Fact]
    public async Task Handle_WhenBarcodeExists_FailsBeforeFetchingShopee()
    {
        Product existing = ProductTestFactory.CreateSingle();
        _productRepository.Setup(r => r.GetByBarcodeAsync("BC-100", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            CreateCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.BarcodeAlreadyExists);
        _gateway.Verify(g => g.GetItemBaseInfoAsync(
            It.IsAny<long>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyCollection<long>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        VerifyNothingCreated();
    }

    [Fact]
    public async Task Handle_WhenSkuExists_Fails()
    {
        Product existing = ProductTestFactory.CreateSingle(skuCode: "LOCAL-SKU");
        _productRepository.Setup(r => r.GetBySkuCodeAsync("LOCAL-SKU", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            CreateCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.SkuAlreadyExists);
        VerifyNothingCreated();
    }

    [Fact]
    public async Task Handle_WhenConnectionMissing_Fails()
    {
        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            CreateCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeProductLinkErrors.ConnectionNotFound);
        VerifyNothingCreated();
    }

    [Fact]
    public async Task Handle_WhenShopeeItemMissing_Fails()
    {
        ShopeeShopConnection connection = CreateConnection();
        SetupConnection(connection);
        _gateway.Setup(g => g.GetItemBaseInfoAsync(
                connection.ShopId,
                connection.AccessToken,
                It.IsAny<IReadOnlyCollection<long>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ShopeeItemDetail>>([]));

        Result<ShopeeProductLinkResponse> result = await _handler.Handle(
            CreateCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeProductLinkErrors.ItemNotFoundInShop);
        VerifyNothingCreated();
    }

    private static CreateShopeeLinkedProductCommand CreateCommand(
        long itemId = 1001,
        long modelId = 0,
        string barcode = "BC-100",
        string name = "Local Widget",
        string? skuCode = "LOCAL-SKU",
        decimal cost = 12.5m,
        int stockQuantity = 9,
        int? lowStockThreshold = null) =>
        new(TenantId, itemId, modelId, barcode, name, skuCode, cost, stockQuantity, lowStockThreshold);

    private void SetupConnection(ShopeeShopConnection connection) =>
        _connectionRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

    private void SetupModellessItem(ShopeeShopConnection connection, int stockQuantity) =>
        _gateway.Setup(g => g.GetItemBaseInfoAsync(
                connection.ShopId,
                connection.AccessToken,
                It.Is<IReadOnlyCollection<long>>(ids => ids.Contains(1001)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ShopeeItemDetail>>([
                new ShopeeItemDetail(1001, "Shopee Item", "SHOPEE-SKU", "NORMAL", false, stockQuantity, null)
            ]));

    private void VerifyNothingCreated()
    {
        _productRepository.Verify(r => r.Add(It.IsAny<Product>()), Times.Never);
        _linkRepository.Verify(r => r.Add(It.IsAny<ShopeeProductLink>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ShopeeShopConnection CreateConnection() =>
        ShopeeShopConnection.Create(
            TenantId,
            123456,
            "access-token",
            "refresh-token",
            DateTime.UtcNow.AddHours(4),
            DateTime.UtcNow.AddDays(30),
            DateTime.UtcNow,
            null).Value;
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build 2>&1 | tail -5`
Expected: build FAILS — `CreateShopeeLinkedProductCommandHandler` does not exist.

- [ ] **Step 3: Write the handler**

Create `src/Wrapsfer.Application/Shopee/Commands/CreateShopeeLinkedProduct/CreateShopeeLinkedProductCommandHandler.cs`.

Note the `TenantSettingsEntity` alias: the entity class name `TenantSettings` collides with the `Wrapsfer.Application.TenantSettings` namespace (same workaround as `CreateProductCommandHandler`).

```csharp
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Products;
using Wrapsfer.Application.Shopee.Common;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.Shopee.Commands.CreateShopeeLinkedProduct;

internal sealed class CreateShopeeLinkedProductCommandHandler(
    IProductRepository productRepository,
    IShopeeProductLinkRepository linkRepository,
    IShopeeShopConnectionRepository connectionRepository,
    ShopeeSellableUnitResolver sellableUnitResolver,
    ITenantSettingsRepository tenantSettingsRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IOptions<ProductSettings> productSettings)
    : ICommandHandler<CreateShopeeLinkedProductCommand, ShopeeProductLinkResponse>
{
    public async Task<Result<ShopeeProductLinkResponse>> Handle(
        CreateShopeeLinkedProductCommand request,
        CancellationToken cancellationToken)
    {
        bool shopeeLinked = await linkRepository.ExistsAsync(
            request.TenantId, request.ShopeeItemId, request.ShopeeModelId, cancellationToken);
        if (shopeeLinked)
        {
            return Result<ShopeeProductLinkResponse>.Failure(ShopeeProductLinkErrors.AlreadyLinked);
        }

        Product? existingByBarcode = await productRepository.GetByBarcodeAsync(
            request.Barcode, request.TenantId, cancellationToken);
        if (existingByBarcode is not null)
        {
            return Result<ShopeeProductLinkResponse>.Failure(ProductErrors.BarcodeAlreadyExists);
        }

        if (!string.IsNullOrWhiteSpace(request.SkuCode))
        {
            Product? existingBySku = await productRepository.GetBySkuCodeAsync(
                request.SkuCode, request.TenantId, cancellationToken);
            if (existingBySku is not null)
            {
                return Result<ShopeeProductLinkResponse>.Failure(ProductErrors.SkuAlreadyExists);
            }
        }

        ShopeeShopConnection? connection =
            await connectionRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (connection is null)
        {
            return Result<ShopeeProductLinkResponse>.Failure(ShopeeProductLinkErrors.ConnectionNotFound);
        }

        Result<ShopeeSellableUnit> unitResult = await sellableUnitResolver.ResolveAsync(
            connection, request.ShopeeItemId, request.ShopeeModelId, cancellationToken);
        if (unitResult.IsFailure)
        {
            return Result<ShopeeProductLinkResponse>.Failure(unitResult.Error);
        }

        Result<Product> productResult = Product.Create(
            request.TenantId,
            request.Barcode,
            request.Name,
            ProductType.Single,
            request.Cost,
            request.StockQuantity,
            request.SkuCode,
            request.LowStockThreshold);
        if (productResult.IsFailure)
        {
            return Result<ShopeeProductLinkResponse>.Failure(productResult.Error);
        }

        Product product = productResult.Value;
        TenantSettingsEntity? settings =
            await tenantSettingsRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        int fallbackThreshold =
            settings?.DefaultLowStockThreshold ?? productSettings.Value.GlobalLowStockThreshold;
        product.CheckLowStock(fallbackThreshold);

        ShopeeSellableUnit unit = unitResult.Value;
        Result<ShopeeProductLink> linkResult = ShopeeProductLink.Create(
            request.TenantId,
            product.Id,
            unit.ItemId,
            unit.ModelId,
            unit.ItemName,
            unit.ModelName,
            unit.Sku,
            tenantContext.Username);
        if (linkResult.IsFailure)
        {
            return Result<ShopeeProductLinkResponse>.Failure(linkResult.Error);
        }

        ShopeeProductLink link = linkResult.Value;
        productRepository.Add(product);
        linkRepository.Add(link);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ShopeeProductLinkResponse>.Success(
            ShopeeProductLinkResponseMapper.Map(link, product));
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build 2>&1 | tail -3 && dotnet test --no-restore --no-build --filter "FullyQualifiedName~CreateShopeeLinkedProductCommandHandlerTests" 2>&1 | tail -5`
Expected: build succeeds; 8 tests PASS.

- [ ] **Step 5: Run the full Application test suite to check for regressions**

Run: `dotnet test --no-restore --no-build tests/Wrapsfer.Application.Tests 2>&1 | tail -5`
Expected: all tests PASS.

- [ ] **Step 6: Format and commit**

```bash
make format-all
git add src/Wrapsfer.Application/Shopee/Commands/CreateShopeeLinkedProduct tests/Wrapsfer.Application.Tests/Shopee/Commands/CreateShopeeLinkedProductCommandHandlerTests.cs
git commit -m "feat: add atomic create-and-link handler for Shopee products"
```

---

### Task 3: Backend — API contract + endpoint (api repo)

**Files:**
- Create: `src/Wrapsfer.Api/Contracts/CreateShopeeLinkedProductRequest.cs`
- Modify: `src/Wrapsfer.Api/Controllers/V1/ShopeeController.cs` (insert a new action after the existing `LinkProduct` action, around line 138)

**Interfaces:**
- Consumes: `CreateShopeeLinkedProductCommand` (Task 1); `ToCreatedResult` from `ApiControllerBase`.
- Produces: `POST api/v1/shopee/{tenantId}/product-links/with-new-product` accepting `CreateShopeeLinkedProductRequest` JSON body `{ shopeeItemId, shopeeModelId, barcode, name, skuCode, cost, stockQuantity, lowStockThreshold }`, returning 201 with `ShopeeProductLinkResponse`. Task 4's frontend service calls exactly this path and body shape.

All commands in this task run from `/home/vernon/Projects/Wrapsfer/api`.

There are no controller unit tests in this codebase (controllers are thin `sender.Send` pass-throughs), so this task's verification is a clean build plus the existing suite.

- [ ] **Step 1: Create the request contract**

Create `src/Wrapsfer.Api/Contracts/CreateShopeeLinkedProductRequest.cs`:

```csharp
namespace Wrapsfer.Api.Contracts;

public sealed record CreateShopeeLinkedProductRequest(
    long ShopeeItemId,
    long ShopeeModelId,
    string Barcode,
    string Name,
    string? SkuCode,
    decimal Cost,
    int StockQuantity,
    int? LowStockThreshold);
```

- [ ] **Step 2: Add the controller action**

In `src/Wrapsfer.Api/Controllers/V1/ShopeeController.cs`:

Add to the using block (keep alphabetical order with the other `Wrapsfer.Application.Shopee.Commands.*` usings):

```csharp
using Wrapsfer.Application.Shopee.Commands.CreateShopeeLinkedProduct;
```

Insert this action directly after the existing `LinkProduct` action (after its closing brace, before `UnlinkProduct`):

```csharp
    [HttpPost("{tenantId}/product-links/with-new-product")]
    public async Task<IActionResult> CreateLinkedProduct(
        string tenantId,
        [FromBody] CreateShopeeLinkedProductRequest request,
        CancellationToken cancellationToken)
    {
        Result<ShopeeProductLinkResponse> result = await sender.Send(
            new CreateShopeeLinkedProductCommand(
                tenantId,
                request.ShopeeItemId,
                request.ShopeeModelId,
                request.Barcode,
                request.Name,
                request.SkuCode,
                request.Cost,
                request.StockQuantity,
                request.LowStockThreshold),
            cancellationToken);
        return ToCreatedResult(result);
    }
```

- [ ] **Step 3: Build and run the full test suite**

Run: `dotnet build 2>&1 | tail -3 && dotnet test --no-restore --no-build 2>&1 | tail -5`
Expected: build succeeds; all tests PASS.

- [ ] **Step 4: Format and commit**

```bash
make format-all
git add src/Wrapsfer.Api/Contracts/CreateShopeeLinkedProductRequest.cs src/Wrapsfer.Api/Controllers/V1/ShopeeController.cs
git commit -m "feat: add endpoint to create and link a product from a Shopee item"
```

---

### Task 4: Frontend — payload type + service function (partner repo)

**Files:**
- Modify: `src/features/partner/shopee-products/types.ts` (append)
- Modify: `src/features/partner/shopee-products/service.ts` (append)
- Test: `src/features/partner/shopee-products/service.test.ts` (append)

**Interfaces:**
- Consumes: Task 3's endpoint `POST /api/v1/shopee/{tenantId}/product-links/with-new-product`; existing `api` wrapper, `shopeePath` helper, `ShopeeProductLink` type.
- Produces: `CreateShopeeLinkedProductPayload` interface and `createShopeeLinkedProduct(tenantId: string, payload: CreateShopeeLinkedProductPayload): Promise<ShopeeProductLink>` — Task 5's form submits through this function.

All commands in this task run from `/home/vernon/Projects/Wrapsfer/partner`.

- [ ] **Step 1: Write the failing service test**

In `src/features/partner/shopee-products/service.test.ts`, add `createShopeeLinkedProduct` to the existing import from `@/features/partner/shopee-products/service` (keep alphabetical order: it comes first), then append this describe block at the end of the file:

```ts
describe("createShopeeLinkedProduct", () => {
	it("posts the new product fields with the shopee ids", async () => {
		mockedPost.mockResolvedValue({ data: link, response: new Response() });

		const result = await createShopeeLinkedProduct("partner-alpha", {
			shopeeItemId: 1001,
			shopeeModelId: 0,
			barcode: "BOX-1",
			name: "Mailer Box",
			skuCode: "SKU-1",
			cost: 2.5,
			stockQuantity: 7,
			lowStockThreshold: null,
		});

		expect(mockedPost).toHaveBeenCalledWith(
			"/api/v1/shopee/partner-alpha/product-links/with-new-product",
			{
				shopeeItemId: 1001,
				shopeeModelId: 0,
				barcode: "BOX-1",
				name: "Mailer Box",
				skuCode: "SKU-1",
				cost: 2.5,
				stockQuantity: 7,
				lowStockThreshold: null,
			},
		);
		expect(result).toEqual(link);
	});
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm test src/features/partner/shopee-products/service.test.ts`
Expected: FAIL — `createShopeeLinkedProduct` is not exported from the service module.

- [ ] **Step 3: Add the payload type and service function**

Append to `src/features/partner/shopee-products/types.ts`:

```ts
export interface CreateShopeeLinkedProductPayload {
	shopeeItemId: number;
	shopeeModelId: number;
	barcode: string;
	name: string;
	skuCode: string | null;
	cost: number;
	stockQuantity: number;
	lowStockThreshold: number | null;
}
```

In `src/features/partner/shopee-products/service.ts`, add `CreateShopeeLinkedProductPayload` to the existing type import from `@/features/partner/shopee-products/types` (alphabetical: it comes first), then append:

```ts
export async function createShopeeLinkedProduct(
	tenantId: string,
	payload: CreateShopeeLinkedProductPayload,
): Promise<ShopeeProductLink> {
	const { data } = await api.post<ShopeeProductLink>(
		`${shopeePath(tenantId)}/product-links/with-new-product`,
		payload,
	);
	return data;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `pnpm test src/features/partner/shopee-products/service.test.ts`
Expected: all tests in the file PASS, including the new one.

- [ ] **Step 5: Lint and commit**

```bash
pnpm check
git add src/features/partner/shopee-products
git commit -m "feat: add create-linked-product service call for Shopee items"
```

---

### Task 5: Frontend — form schema + drawer components (partner repo)

**Files:**
- Create: `src/features/partner/shopee-products/schema.ts`
- Create: `src/features/partner/shopee-products/components/create-linked-product-form.tsx`
- Create: `src/features/partner/shopee-products/components/create-linked-product-drawer.tsx`

**Interfaces:**
- Consumes: `createShopeeLinkedProduct` + `CreateShopeeLinkedProductPayload` (Task 4); existing `ShopeeSellableUnit` type, `shopeeItemsQueryOptions` / `shopeeProductLinksQueryOptions`, `getApiErrorDetail`.
- Produces: `CreateLinkedProductDrawer({ tenantId, unit, currentOffset, onClose })` — Task 6 renders it from the page. The form component is internal to the drawer.

The drawer follows the `LinkProductDrawer` open/close pattern (`opened={Boolean(unit)}`). The form is a separate component mounted with a `key` per sellable unit so `useForm` initial values re-derive from the Shopee unit each time a different row is opened (no `useEffect` sync needed).

All commands in this task run from `/home/vernon/Projects/Wrapsfer/partner`.

- [ ] **Step 1: Create the Zod schema**

Create `src/features/partner/shopee-products/schema.ts`:

```ts
import type { IntlShape } from "react-intl";
import { z } from "zod";

export function createLinkedProductSchema(intl: IntlShape) {
	return z.object({
		barcode: z
			.string()
			.min(
				1,
				intl.formatMessage({
					id: "partner.shopeeProducts.create.errors.barcodeRequired",
					defaultMessage: "Barcode is required",
				}),
			)
			.max(256),
		name: z.string().min(
			1,
			intl.formatMessage({
				id: "partner.shopeeProducts.create.errors.nameRequired",
				defaultMessage: "Name is required",
			}),
		),
		skuCode: z.string().max(256),
		cost: z.number().min(
			0,
			intl.formatMessage({
				id: "partner.shopeeProducts.create.errors.costMin",
				defaultMessage: "Cost must be 0 or greater",
			}),
		),
		stockQuantity: z
			.number()
			.int()
			.min(
				0,
				intl.formatMessage({
					id: "partner.shopeeProducts.create.errors.stockMin",
					defaultMessage: "Stock must be 0 or greater",
				}),
			),
		lowStockThreshold: z.number().int().min(0).optional(),
	});
}

export type CreateLinkedProductFormValues = z.infer<
	ReturnType<typeof createLinkedProductSchema>
>;
```

- [ ] **Step 2: Create the form component**

Create `src/features/partner/shopee-products/components/create-linked-product-form.tsx`:

```tsx
import {
	Alert,
	Button,
	Group,
	NumberInput,
	Stack,
	Text,
	TextInput,
} from "@mantine/core";
import { useForm } from "@mantine/form";
import { notifications } from "@mantine/notifications";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AlertCircle } from "lucide-react";
import { zodResolver } from "mantine-form-zod-resolver";
import { useMemo } from "react";
import { FormattedMessage, useIntl } from "react-intl";
import {
	shopeeItemsQueryOptions,
	shopeeProductLinksQueryOptions,
} from "@/features/partner/shopee-products/query-options";
import {
	type CreateLinkedProductFormValues,
	createLinkedProductSchema,
} from "@/features/partner/shopee-products/schema";
import { createShopeeLinkedProduct } from "@/features/partner/shopee-products/service";
import type { ShopeeSellableUnit } from "@/features/partner/shopee-products/types";
import { getApiErrorDetail } from "@/lib/api";

interface CreateLinkedProductFormProps {
	tenantId: string;
	unit: ShopeeSellableUnit;
	currentOffset: number;
	onClose: () => void;
}

export function CreateLinkedProductForm({
	tenantId,
	unit,
	currentOffset,
	onClose,
}: CreateLinkedProductFormProps) {
	const intl = useIntl();
	const queryClient = useQueryClient();

	const schema = useMemo(() => createLinkedProductSchema(intl), [intl]);

	const form = useForm<CreateLinkedProductFormValues>({
		validate: zodResolver(schema),
		initialValues: {
			barcode: "",
			name: unit.modelName ?? unit.itemName,
			skuCode: unit.sku ?? "",
			cost: 0,
			stockQuantity: unit.stockQuantity ?? 0,
			lowStockThreshold: undefined,
		},
	});

	const mutation = useMutation({
		mutationFn: (values: CreateLinkedProductFormValues) =>
			createShopeeLinkedProduct(tenantId, {
				shopeeItemId: unit.itemId,
				shopeeModelId: unit.modelId,
				barcode: values.barcode,
				name: values.name,
				skuCode: values.skuCode.trim().length > 0 ? values.skuCode : null,
				cost: values.cost,
				stockQuantity: values.stockQuantity,
				lowStockThreshold: values.lowStockThreshold ?? null,
			}),
		onSuccess: () => {
			notifications.show({
				color: "green",
				message: intl.formatMessage({
					id: "partner.shopeeProducts.create.success",
					defaultMessage: "Product created and linked",
				}),
			});
			void queryClient.invalidateQueries({
				queryKey: shopeeProductLinksQueryOptions(tenantId).queryKey,
			});
			void queryClient.invalidateQueries({
				queryKey: shopeeItemsQueryOptions(tenantId, {
					offset: currentOffset,
					pageSize: 20,
				}).queryKey,
			});
			onClose();
		},
	});

	return (
		<form onSubmit={form.onSubmit((values) => mutation.mutate(values))}>
			<Stack gap="md">
				{mutation.isError && (
					<Alert role="alert" color="red" icon={<AlertCircle size={16} />}>
						{getApiErrorDetail(mutation.error) ?? (
							<FormattedMessage
								id="partner.shopeeProducts.create.error"
								defaultMessage="Failed to create the product. Please try again."
							/>
						)}
					</Alert>
				)}
				<Stack gap={4}>
					<Text fw={600} lineClamp={2}>
						{unit.modelName ?? unit.itemName}
					</Text>
					<Text c="dimmed" size="sm" lineClamp={1}>
						{unit.sku ?? (
							<FormattedMessage
								id="partner.shopeeProducts.common.noSku"
								defaultMessage="No SKU"
							/>
						)}
					</Text>
				</Stack>
				<TextInput
					label={intl.formatMessage({
						id: "partner.shopeeProducts.create.barcode",
						defaultMessage: "Barcode",
					})}
					required
					{...form.getInputProps("barcode")}
				/>
				<TextInput
					label={intl.formatMessage({
						id: "partner.shopeeProducts.create.name",
						defaultMessage: "Name",
					})}
					required
					{...form.getInputProps("name")}
				/>
				<TextInput
					label={intl.formatMessage({
						id: "partner.shopeeProducts.create.skuCode",
						defaultMessage: "SKU Code",
					})}
					{...form.getInputProps("skuCode")}
				/>
				<NumberInput
					label={intl.formatMessage({
						id: "partner.shopeeProducts.create.cost",
						defaultMessage: "Cost",
					})}
					min={0}
					decimalScale={2}
					required
					{...form.getInputProps("cost")}
				/>
				<NumberInput
					label={intl.formatMessage({
						id: "partner.shopeeProducts.create.stockQuantity",
						defaultMessage: "Stock Quantity",
					})}
					description={intl.formatMessage({
						id: "partner.shopeeProducts.create.stockQuantityHint",
						defaultMessage: "Pre-filled from Shopee stock; the next sync pushes this value to Shopee.",
					})}
					min={0}
					required
					{...form.getInputProps("stockQuantity")}
				/>
				<NumberInput
					label={intl.formatMessage({
						id: "partner.shopeeProducts.create.lowStockThreshold",
						defaultMessage: "Low Stock Threshold",
					})}
					min={0}
					{...form.getInputProps("lowStockThreshold")}
				/>
				<Group justify="flex-end" mt="sm">
					<Button variant="default" onClick={onClose}>
						<FormattedMessage id="common.cancel" defaultMessage="Cancel" />
					</Button>
					<Button type="submit" loading={mutation.isPending}>
						<FormattedMessage
							id="partner.shopeeProducts.create.confirm"
							defaultMessage="Create and link"
						/>
					</Button>
				</Group>
			</Stack>
		</form>
	);
}
```

- [ ] **Step 3: Create the drawer component**

Create `src/features/partner/shopee-products/components/create-linked-product-drawer.tsx`:

```tsx
import { Drawer, Text } from "@mantine/core";
import { FormattedMessage } from "react-intl";
import { CreateLinkedProductForm } from "@/features/partner/shopee-products/components/create-linked-product-form";
import type { ShopeeSellableUnit } from "@/features/partner/shopee-products/types";

interface CreateLinkedProductDrawerProps {
	tenantId: string;
	unit: ShopeeSellableUnit | null;
	currentOffset: number;
	onClose: () => void;
}

export function CreateLinkedProductDrawer({
	tenantId,
	unit,
	currentOffset,
	onClose,
}: CreateLinkedProductDrawerProps) {
	return (
		<Drawer
			opened={Boolean(unit)}
			onClose={onClose}
			position="right"
			title={
				<Text component="span" fw={700} size="lg">
					<FormattedMessage
						id="partner.shopeeProducts.create.title"
						defaultMessage="Create product from Shopee"
					/>
				</Text>
			}
		>
			{unit && (
				<CreateLinkedProductForm
					key={`${unit.itemId}:${unit.modelId}`}
					tenantId={tenantId}
					unit={unit}
					currentOffset={currentOffset}
					onClose={onClose}
				/>
			)}
		</Drawer>
	);
}
```

- [ ] **Step 4: Verify typecheck and lint**

Run: `pnpm typecheck && pnpm check`
Expected: no type errors; biome applies/reports no remaining issues.

- [ ] **Step 5: Commit**

```bash
git add src/features/partner/shopee-products
git commit -m "feat: add create-linked-product drawer for Shopee items"
```

---

### Task 6: Frontend — wire the row action, page state, i18n (partner repo)

**Files:**
- Modify: `src/features/partner/shopee-products/components/shopee-unit-row.tsx`
- Modify: `src/features/partner/shopee-products/components/shopee-items-table.tsx`
- Modify: `src/features/partner/shopee-products/index.tsx`
- Modify (generated): `src/locales/*.json` via `pnpm extract`

**Interfaces:**
- Consumes: `CreateLinkedProductDrawer` (Task 5); existing `ShopeeSellableUnit`, `ShopeeUnitRow`, `ShopeeItemsTable`, `ShopeeProductsPage`.
- Produces: an `onCreateProduct: (unit: ShopeeSellableUnit) => void` prop threaded page → table → row; a `PackagePlus` icon action on every unlinked, linkable row.

All commands in this task run from `/home/vernon/Projects/Wrapsfer/partner`.

- [ ] **Step 1: Add the create action to the unit row**

In `src/features/partner/shopee-products/components/shopee-unit-row.tsx`:

1. Change the lucide import to include `PackagePlus`:

```tsx
import { Link, PackagePlus, RefreshCw, Unlink } from "lucide-react";
```

2. Add the prop to `ShopeeUnitRowProps` (after `onLink`):

```tsx
	onCreateProduct: (unit: ShopeeSellableUnit) => void;
```

3. Add `onCreateProduct` to the destructured props of `ShopeeUnitRow` (after `onLink`).

4. Build the sellable unit once, so both actions share it. Immediately after the `const intl = useIntl();` line inside `ShopeeUnitRow`, add:

```tsx
	const unit: ShopeeSellableUnit = {
		itemId: item.itemId,
		modelId,
		itemName: item.itemName,
		modelName: isModel ? name : null,
		sku,
		stockQuantity,
		link: null,
	};
```

5. In the unlinked branch (currently `canLink && (<Tooltip ...link action... />)`), replace the whole `canLink && (...)` expression with:

```tsx
						canLink && (
							<>
								<Tooltip
									label={intl.formatMessage({
										id: "partner.shopeeProducts.actions.link",
										defaultMessage: "Link",
									})}
								>
									<ActionIcon
										variant="subtle"
										color="teal"
										onClick={() => onLink(unit)}
										aria-label={intl.formatMessage({
											id: "partner.shopeeProducts.actions.link",
											defaultMessage: "Link",
										})}
									>
										<Link size={16} />
									</ActionIcon>
								</Tooltip>
								<Tooltip
									label={intl.formatMessage({
										id: "partner.shopeeProducts.actions.createProduct",
										defaultMessage: "Create product",
									})}
								>
									<ActionIcon
										variant="subtle"
										color="teal"
										onClick={() => onCreateProduct(unit)}
										aria-label={intl.formatMessage({
											id: "partner.shopeeProducts.actions.createProduct",
											defaultMessage: "Create product",
										})}
									>
										<PackagePlus size={16} />
									</ActionIcon>
								</Tooltip>
							</>
						)
```

- [ ] **Step 2: Thread the prop through the table**

In `src/features/partner/shopee-products/components/shopee-items-table.tsx`:

1. Add to `ShopeeItemsTableProps` (after `onLink`):

```tsx
	onCreateProduct: (unit: ShopeeSellableUnit) => void;
```

2. Add `onCreateProduct` to the destructured props of `ShopeeItemsTable` (after `onLink`).

3. Pass `onCreateProduct={onCreateProduct}` to **both** `<ShopeeUnitRow ...>` usages (the item row and the model row), next to `onLink={onLink}`.

- [ ] **Step 3: Add page state and render the drawer**

In `src/features/partner/shopee-products/index.tsx`:

1. Add to the component imports:

```tsx
import { CreateLinkedProductDrawer } from "@/features/partner/shopee-products/components/create-linked-product-drawer";
```

2. Add state next to the existing `linkUnit` state:

```tsx
	const [createUnit, setCreateUnit] = useState<ShopeeSellableUnit | null>(null);
```

3. Pass the handler to the table, next to `onLink={setLinkUnit}`:

```tsx
									onCreateProduct={setCreateUnit}
```

4. Render the drawer next to `<LinkProductDrawer ...>`:

```tsx
			<CreateLinkedProductDrawer
				tenantId={tenantId}
				unit={createUnit}
				currentOffset={offset}
				onClose={() => setCreateUnit(null)}
			/>
```

- [ ] **Step 4: Extract i18n messages**

Run: `pnpm extract`
Expected: `src/locales/en.json` gains the new `partner.shopeeProducts.create.*` and `partner.shopeeProducts.actions.createProduct` keys; other locale files gain the same keys with `""` values.

- [ ] **Step 5: Full frontend verification**

Run: `pnpm typecheck && pnpm check && pnpm test`
Expected: no type errors, no lint issues, all Vitest tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/features/partner/shopee-products src/locales src/features/partner/shopee-products/index.tsx
git commit -m "feat: wire create-product-from-Shopee action into the Shopee products page"
```

---

## Final Verification (both repos)

- [ ] api: `cd /home/vernon/Projects/Wrapsfer/api && make build && make test` — everything green.
- [ ] partner: `cd /home/vernon/Projects/Wrapsfer/partner && pnpm typecheck && pnpm check && pnpm test` — everything green.
- [ ] Manual smoke (optional, needs local infra + a connected Shopee shop): open the Shopee products page, pick an unlinked row → "Create product" → confirm the form pre-fills name/SKU/stock, requires barcode and cost, and on submit the row shows the new linked product badge.
