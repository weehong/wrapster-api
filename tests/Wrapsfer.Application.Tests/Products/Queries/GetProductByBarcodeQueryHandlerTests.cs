using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Products.Queries.GetProductByBarcode;
using Wrapsfer.Application.Products.Responses;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Products.Queries;

public class GetProductByBarcodeQueryHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly GetProductByBarcodeQueryHandler _handler;
    private readonly Mock<IProductComponentRepository> _productComponentRepository = new();

    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();

    public GetProductByBarcodeQueryHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _productComponentRepository
            .Setup(r => r.GetByParentIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _handler = new GetProductByBarcodeQueryHandler(
            _productRepository.Object, _productComponentRepository.Object, _tenantContext.Object);
    }

    [Fact]
    public async Task Handle_WhenProductNotFound_ReturnsNotFoundFailure()
    {
        _productRepository.Setup(r => r.GetByBarcodeAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        Result<ProductResponse> result = await _handler.Handle(
            new GetProductByBarcodeQuery("BC-999"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenSingleProduct_ReturnsResponseWithActualStock()
    {
        Product product = ProductTestFactory.CreateSingle(barcode: "BC-001", stockQuantity: 42);
        _productRepository.Setup(r => r.GetByBarcodeAsync("BC-001", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        Result<ProductResponse> result = await _handler.Handle(
            new GetProductByBarcodeQuery("BC-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StockQuantity.Should().Be(42);
    }

    [Fact]
    public async Task Handle_WhenBundleProduct_ReturnsResponseWithVirtualStock()
    {
        Product bundle = ProductTestFactory.CreateBundle(barcode: "BC-BUNDLE");
        _productRepository.Setup(r => r.GetByBarcodeAsync("BC-BUNDLE", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);
        _productRepository.Setup(r => r.GetBundleComponentDataAsync(
                It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<(int ChildStock, int Ratio)>>
            {
                [bundle.Id] = new List<(int, int)> { (20, 2) }
            });

        Result<ProductResponse> result = await _handler.Handle(
            new GetProductByBarcodeQuery("BC-BUNDLE"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StockQuantity.Should().Be(10);
    }
}
