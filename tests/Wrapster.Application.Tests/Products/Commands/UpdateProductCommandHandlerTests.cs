using Wrapster.Application.Abstractions;
using Wrapster.Application.Products;
using Wrapster.Application.Products.Commands.UpdateProduct;
using Wrapster.Application.Tests.Helpers;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Tests.Products.Commands;

public class UpdateProductCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly UpdateProductCommandHandler _handler;

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
        _handler = new UpdateProductCommandHandler(_productRepository.Object, _tenantSettingsRepository.Object,
            _tenantContext.Object, _unitOfWork.Object, settings);
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
}
