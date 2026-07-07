using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Products.Queries.GetProductBySku;
using Wrapsfer.Application.Products.Responses;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Products.Queries;

public class GetProductBySkuQueryHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly GetProductBySkuQueryHandler _handler;
    private readonly Mock<IProductComponentRepository> _productComponentRepository = new();

    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();

    public GetProductBySkuQueryHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _productComponentRepository
            .Setup(r => r.GetByParentIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _handler = new GetProductBySkuQueryHandler(
            _productRepository.Object, _productComponentRepository.Object, _tenantContext.Object);
    }

    [Fact]
    public async Task Handle_WhenProductNotFound_ReturnsNotFoundFailure()
    {
        _productRepository.Setup(r => r.GetBySkuCodeAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        Result<ProductResponse> result = await _handler.Handle(
            new GetProductBySkuQuery("SKU-999"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenSingleProduct_ReturnsResponseWithActualStock()
    {
        Product product = ProductTestFactory.CreateSingle(skuCode: "SKU-1", stockQuantity: 42);
        _productRepository.Setup(r => r.GetBySkuCodeAsync("SKU-1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        Result<ProductResponse> result = await _handler.Handle(
            new GetProductBySkuQuery("SKU-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StockQuantity.Should().Be(42);
    }

    [Fact]
    public async Task Handle_WhenBundleProduct_ReturnsResponseWithVirtualStock()
    {
        Product bundle = ProductTestFactory.CreateBundle(skuCode: "SKU-BUNDLE");
        _productRepository.Setup(r => r.GetBySkuCodeAsync("SKU-BUNDLE", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);
        _productRepository.Setup(r => r.GetBundleComponentDataAsync(
                It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<(int ChildStock, int Ratio)>>
            {
                [bundle.Id] = new List<(int, int)> { (15, 5) }
            });

        Result<ProductResponse> result = await _handler.Handle(
            new GetProductBySkuQuery("SKU-BUNDLE"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StockQuantity.Should().Be(3);
    }
}
