using Wrapster.Application.Abstractions;
using Wrapster.Application.Products;
using Wrapster.Application.Products.Commands.CreateProduct;
using Wrapster.Application.Tests.Helpers;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Enums;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Tests.Products.Commands;

public class CreateProductCommandHandlerTests
{
    private const string TenantId = "test-tenant";

    private readonly Mock<IProductComponentRepository> _componentRepository = new();
    private readonly CreateProductCommandHandler _handler;
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<ITenantSettingsRepository> _tenantSettingsRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public CreateProductCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.TenantSettings?)null);
        IOptions<ProductSettings> settings = Options.Create(new ProductSettings { GlobalLowStockThreshold = 10 });
        _handler = new CreateProductCommandHandler(_productRepository.Object, _componentRepository.Object,
            _tenantSettingsRepository.Object, _tenantContext.Object, _unitOfWork.Object, settings);
    }

    [Fact]
    public async Task Handle_WhenBarcodeExists_ReturnsBarcodeAlreadyExistsFailure()
    {
        Product existing = ProductTestFactory.CreateSingle();
        _productRepository.Setup(r => r.GetByBarcodeAsync("BC-001", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        CreateProductCommand command = new("BC-001", "Widget", null, ProductType.Single, 10m, 50, null);

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.BarcodeAlreadyExists.Code);
    }

    [Fact]
    public async Task Handle_WhenSkuExists_ReturnsSkuAlreadyExistsFailure()
    {
        Product existing = ProductTestFactory.CreateSingle(skuCode: "SKU-1");
        _productRepository.Setup(r => r.GetByBarcodeAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        _productRepository.Setup(r => r.GetBySkuCodeAsync("SKU-1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        CreateProductCommand command = new("BC-001", "Widget", "SKU-1", ProductType.Single, 10m, 50, null);

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.SkuAlreadyExists.Code);
    }

    [Fact]
    public async Task Handle_WhenPackageWithNonExistentTarget_ReturnsInvalidUnpackTargetFailure()
    {
        _productRepository.Setup(r => r.GetByBarcodeAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        Guid targetId = Guid.NewGuid();
        CreateProductCommand command = new("BC-001", "Package", null, ProductType.Package, 50m, 10, null,
            targetId, 6);

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidUnpackTarget.Code);
    }

    [Fact]
    public async Task Handle_WhenPackageWithNonSingleTarget_ReturnsInvalidUnpackTargetFailure()
    {
        Product bundleTarget = ProductTestFactory.CreateBundle();
        _productRepository.Setup(r => r.GetByBarcodeAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundleTarget);

        CreateProductCommand command = new("BC-001", "Package", null, ProductType.Package, 50m, 10, null,
            Guid.NewGuid(), 6);

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidUnpackTarget.Code);
    }

    [Fact]
    public async Task Handle_WhenValid_CreatesProductAndReturnsId()
    {
        _productRepository.Setup(r => r.GetByBarcodeAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        CreateProductCommand command = new("BC-001", "Widget", null, ProductType.Single, 10m, 50, null);

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _productRepository.Verify(r => r.Add(It.IsAny<Product>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPackageWithValidTarget_CreatesProduct()
    {
        Product singleTarget = ProductTestFactory.CreateSingle();
        _productRepository.Setup(r => r.GetByBarcodeAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(singleTarget);

        CreateProductCommand command = new("BC-001", "Package", null, ProductType.Package, 50m, 10, null,
            singleTarget.Id, 6);

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _productRepository.Verify(r => r.Add(It.IsAny<Product>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenBundleWithValidComponents_CreatesProductAndComponents()
    {
        Product childA = ProductTestFactory.CreateSingle();
        Product childB = ProductTestFactory.CreateSingle();

        _productRepository.Setup(r => r.GetByBarcodeAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        _productRepository.Setup(r =>
                r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { childA, childB });

        CreateProductCommand command = new("BC-BUNDLE", "Starter Kit", null, ProductType.Bundle, 25m, 0, null,
            Components: [new BundleComponentInput(childA.Id, 2), new BundleComponentInput(childB.Id, 3)]);

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _productRepository.Verify(r => r.Add(It.IsAny<Product>()), Times.Once);
        _componentRepository.Verify(r => r.Add(It.IsAny<ProductComponent>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenBundleWithNonSingleChild_ReturnsInvalidComponentTypeFailure()
    {
        Product bundleChild = ProductTestFactory.CreateBundle();

        _productRepository.Setup(r => r.GetByBarcodeAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        _productRepository.Setup(r =>
                r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { bundleChild });

        CreateProductCommand command = new("BC-BUNDLE", "Kit", null, ProductType.Bundle, 25m, 0, null,
            Components: [new BundleComponentInput(bundleChild.Id, 2)]);

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidComponentType.Code);
    }

    [Fact]
    public async Task Handle_WhenBundleWithDuplicateComponents_ReturnsDuplicateFailure()
    {
        Guid childId = Guid.NewGuid();

        _productRepository.Setup(r => r.GetByBarcodeAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        CreateProductCommand command = new("BC-BUNDLE", "Kit", null, ProductType.Bundle, 25m, 0, null,
            Components: [new BundleComponentInput(childId, 2), new BundleComponentInput(childId, 3)]);

        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.DuplicateComponentChildId.Code);
    }
}
