using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Products;
using Wrapsfer.Application.Products.Commands.UpdateProductStock;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Products.Commands;

public class UpdateProductStockCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly UpdateProductStockCommandHandler _handler;

    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<ITenantSettingsRepository> _tenantSettingsRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public UpdateProductStockCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _tenantSettingsRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.TenantSettings?)null);
        IOptions<ProductSettings> settings = Options.Create(new ProductSettings { GlobalLowStockThreshold = 10 });
        _handler = new UpdateProductStockCommandHandler(_productRepository.Object, _tenantSettingsRepository.Object,
            _tenantContext.Object, _unitOfWork.Object, settings);
    }

    [Fact]
    public async Task Handle_WhenProductNotFound_ReturnsNotFoundFailure()
    {
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        Result result =
            await _handler.Handle(new UpdateProductStockCommand(Guid.NewGuid(), 50), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenValid_UpdatesStockAndReturnsSuccess()
    {
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 10);
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        Result result = await _handler.Handle(new UpdateProductStockCommand(product.Id, 99), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.StockQuantity.Should().Be(99);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenBundleType_ReturnsCannotSetBundleStockFailure()
    {
        Product bundle = ProductTestFactory.CreateBundle();
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);

        Result result = await _handler.Handle(new UpdateProductStockCommand(bundle.Id, 10), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.CannotSetBundleStock.Code);
    }
}
