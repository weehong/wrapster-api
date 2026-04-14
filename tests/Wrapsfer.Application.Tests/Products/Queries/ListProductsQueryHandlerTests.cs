using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.Products.Queries.ListProducts;
using Wrapsfer.Application.Products.Responses;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Products.Queries;

public class ListProductsQueryHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly ListProductsQueryHandler _handler;

    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();

    public ListProductsQueryHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _handler = new ListProductsQueryHandler(_productRepository.Object, _tenantContext.Object);
    }

    [Fact]
    public async Task Handle_WhenNoProducts_ReturnsEmptyPagedResult()
    {
        _productRepository.Setup(r => r.ListAsync(TenantId, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Product>() as IReadOnlyList<Product>, 0));

        Result<PagedResult<ProductResponse>> result = await _handler.Handle(
            new ListProductsQuery(null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenMixedTypes_BundlesGetVirtualStockAndSinglesGetActualStock()
    {
        Product single = ProductTestFactory.CreateSingle(stockQuantity: 42);
        Product bundle = ProductTestFactory.CreateBundle(stockQuantity: 0);

        _productRepository.Setup(r => r.ListAsync(TenantId, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Product> { single, bundle } as IReadOnlyList<Product>, 2));

        _productRepository.Setup(r => r.GetBundleComponentDataAsync(
                It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<(int ChildStock, int Ratio)>>
            {
                [bundle.Id] = new List<(int, int)> { (20, 4) }
            });

        Result<PagedResult<ProductResponse>> result = await _handler.Handle(
            new ListProductsQuery(null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);

        ProductResponse singleResponse = result.Value.Items.First(r => r.Type == ProductType.Single);
        singleResponse.StockQuantity.Should().Be(42);

        ProductResponse bundleResponse = result.Value.Items.First(r => r.Type == ProductType.Bundle);
        bundleResponse.StockQuantity.Should().Be(5); // floor(20/4)
    }

    [Fact]
    public async Task Handle_WhenNoBundles_SkipsBundleComponentDataFetch()
    {
        Product single = ProductTestFactory.CreateSingle(stockQuantity: 10);

        _productRepository.Setup(r => r.ListAsync(TenantId, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Product> { single } as IReadOnlyList<Product>, 1));

        Result<PagedResult<ProductResponse>> result = await _handler.Handle(
            new ListProductsQuery(null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _productRepository.Verify(r => r.GetBundleComponentDataAsync(
            It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()), Times.Never);
    }
}
