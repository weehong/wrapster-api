using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Products;
using Wrapsfer.Application.Products.Commands.UpdateProduct;
using Wrapsfer.Application.Products.Common;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Products.Commands;

public class UpdateProductCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly UpdateProductCommandHandler _handler;

    private readonly Mock<IProductComponentRepository> _productComponentRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<ITenantSettingsRepository> _tenantSettingsRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public UpdateProductCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.TenantSettings?)null);
        IOptions<ProductSettings> settings = Options.Create(new ProductSettings { GlobalLowStockThreshold = 10 });
        _handler = new UpdateProductCommandHandler(_productRepository.Object, _productComponentRepository.Object,
            _tenantSettingsRepository.Object, _tenantContext.Object, _unitOfWork.Object, settings);
    }

    [Fact]
    public async Task Handle_WhenProductNotFound_ReturnsNotFoundFailure()
    {
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        UpdateProductCommand command = new(Guid.NewGuid(), "Name", null, false, null, null, false);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenSkuConflict_ReturnsSkuAlreadyExistsFailure()
    {
        Product product = ProductTestFactory.CreateSingle(skuCode: "OLD-SKU");
        Product existingBySku = ProductTestFactory.CreateSingle(skuCode: "NEW-SKU");

        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _productRepository.Setup(r => r.GetBySkuCodeAsync("NEW-SKU", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBySku);

        UpdateProductCommand command = new(product.Id, null, "NEW-SKU", false, null, null, false);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.SkuAlreadyExists.Code);
    }

    [Fact]
    public async Task Handle_WhenValid_UpdatesProductAndReturnsSuccess()
    {
        Product product = ProductTestFactory.CreateSingle(name: "Old Name");
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        UpdateProductCommand command = new(product.Id, "New Name", null, false, 20m, null, false);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.Name.Should().Be("New Name");
        product.Cost.Should().Be(20m);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPackageWithInvalidUnpackTarget_ReturnsFailure()
    {
        Guid targetId = Guid.NewGuid();
        Product package = ProductTestFactory.CreatePackage(unpackTargetProductId: targetId);

        _productRepository.Setup(r => r.GetByIdAsync(package.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        Guid newTargetId = Guid.NewGuid();
        _productRepository.Setup(r => r.GetByIdAsync(newTargetId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        UpdateProductCommand command = new(package.Id, null, null, false, null, null, false, newTargetId, 12);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidUnpackTarget.Code);
    }

    [Fact]
    public async Task Handle_WhenPackageWithValidUnpackConfig_UpdatesConfigAndReturnsSuccess()
    {
        Guid targetId = Guid.NewGuid();
        Product package = ProductTestFactory.CreatePackage(unpackTargetProductId: targetId);
        Product newTarget = ProductTestFactory.CreateSingle();

        _productRepository.Setup(r => r.GetByIdAsync(package.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        _productRepository.Setup(r => r.GetByIdAsync(newTarget.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newTarget);

        UpdateProductCommand command = new(package.Id, null, null, false, null, null, false, newTarget.Id, 12);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        package.UnpackTargetProductId.Should().Be(newTarget.Id);
        package.UnpackQuantityPerPackage.Should().Be(12);
    }

    [Fact]
    public async Task Handle_WhenComponentsProvidedOnNonBundle_ReturnsComponentsOnNonBundleFailure()
    {
        Product single = ProductTestFactory.CreateSingle();
        _productRepository.Setup(r => r.GetByIdAsync(single.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(single);

        List<BundleComponentInput> components = [new(Guid.NewGuid(), 1)];
        UpdateProductCommand command = new(single.Id, null, null, false, null, null, false,
            Components: components);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.ComponentsOnNonBundle.Code);
    }

    [Fact]
    public async Task Handle_WhenComponentChildNotFound_ReturnsComponentNotFoundFailure()
    {
        Product bundle = ProductTestFactory.CreateBundle();
        Guid missingChildId = Guid.NewGuid();

        _productRepository.Setup(r => r.GetByIdAsync(bundle.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);
        _productRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Product>());

        UpdateProductCommand command = new(bundle.Id, null, null, false, null, null, false,
            Components: [new(missingChildId, 1)]);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.ComponentNotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenComponentChildIsNotSingle_ReturnsInvalidComponentTypeFailure()
    {
        Product bundle = ProductTestFactory.CreateBundle();
        Product nonSingleChild = ProductTestFactory.CreateBundle();

        _productRepository.Setup(r => r.GetByIdAsync(bundle.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);
        _productRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { nonSingleChild });

        UpdateProductCommand command = new(bundle.Id, null, null, false, null, null, false,
            Components: [new(nonSingleChild.Id, 1)]);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InvalidComponentType.Code);
    }

    [Fact]
    public async Task Handle_WhenBundleComponentsValid_ReplacesComponentsAndReturnsSuccess()
    {
        Product bundle = ProductTestFactory.CreateBundle();
        Product childA = ProductTestFactory.CreateSingle();
        Product childB = ProductTestFactory.CreateSingle();

        _productRepository.Setup(r => r.GetByIdAsync(bundle.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);
        _productRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { childA, childB });

        UpdateProductCommand command = new(bundle.Id, null, null, false, null, null, false,
            Components:
            [
                new(childA.Id, 2),
                new(childB.Id, 1)
            ]);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _productComponentRepository.Verify(
            r => r.RemoveAllByParentIdAsync(bundle.Id, TenantId, It.IsAny<CancellationToken>()), Times.Once);
        _productComponentRepository.Verify(r => r.Add(It.IsAny<ProductComponent>()), Times.Exactly(2));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
