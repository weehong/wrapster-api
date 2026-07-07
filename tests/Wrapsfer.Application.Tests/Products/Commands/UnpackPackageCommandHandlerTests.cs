using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Products;
using Wrapsfer.Application.Products.Commands.UnpackPackage;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Products.Commands;

public class UnpackPackageCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly UnpackPackageCommandHandler _handler;

    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<ITenantSettingsRepository> _tenantSettingsRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public UnpackPackageCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.TenantSettings?)null);
        IOptions<ProductSettings> settings = Options.Create(new ProductSettings { GlobalLowStockThreshold = 10 });
        _handler = new UnpackPackageCommandHandler(_productRepository.Object, _tenantSettingsRepository.Object,
            _tenantContext.Object, _unitOfWork.Object, settings);
    }

    [Fact]
    public async Task Handle_WhenProductNotFound_ReturnsNotFoundFailure()
    {
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        Result result = await _handler.Handle(new UnpackPackageCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenNotPackage_ReturnsNotAPackageFailure()
    {
        Product single = ProductTestFactory.CreateSingle();
        _productRepository.Setup(r => r.GetByIdAsync(single.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(single);

        Result result = await _handler.Handle(new UnpackPackageCommand(single.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.NotAPackage.Code);
    }

    [Fact]
    public async Task Handle_WhenInsufficientStock_ReturnsInsufficientStockFailure()
    {
        Product package = ProductTestFactory.CreatePackage(stockQuantity: 2);
        _productRepository.Setup(r => r.GetByIdAsync(package.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        Result result = await _handler.Handle(new UnpackPackageCommand(package.Id, 5), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.InsufficientStock.Code);
    }

    [Fact]
    public async Task Handle_WhenValid_DeductsPackageStockAndRestoresTargetStock()
    {
        Product target = ProductTestFactory.CreateSingle(stockQuantity: 10);
        Product package = ProductTestFactory.CreatePackage(
            stockQuantity: 5, unpackTargetProductId: target.Id, unpackQuantityPerPackage: 6);

        _productRepository.Setup(r => r.GetByIdAsync(package.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        _productRepository.Setup(r => r.GetByIdAsync(target.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        // Unpack 2 packages: package stock 5-2=3, target stock 10+(2*6)=22
        Result result = await _handler.Handle(new UnpackPackageCommand(package.Id, 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        package.StockQuantity.Should().Be(3);
        target.StockQuantity.Should().Be(22);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
