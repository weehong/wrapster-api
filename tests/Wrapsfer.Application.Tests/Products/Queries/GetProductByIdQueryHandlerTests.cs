using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Products.Queries.GetProductById;
using Wrapsfer.Application.Products.Responses;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Products.Queries;

public class GetProductByIdQueryHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly GetProductByIdQueryHandler _handler;
    private readonly Mock<IProductComponentRepository> _productComponentRepository = new();

    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();

    public GetProductByIdQueryHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _productComponentRepository
            .Setup(r => r.GetByParentIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _handler = new GetProductByIdQueryHandler(
            _productRepository.Object, _productComponentRepository.Object, _tenantContext.Object);
    }

    [Fact]
    public async Task Handle_WhenProductNotFound_ReturnsNotFoundFailure()
    {
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        Result<ProductResponse> result = await _handler.Handle(
            new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenSingleProduct_ReturnsResponseWithActualStock()
    {
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 42);
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        Result<ProductResponse> result = await _handler.Handle(
            new GetProductByIdQuery(product.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StockQuantity.Should().Be(42);
        result.Value.Type.Should().Be(ProductType.Single);
    }

    [Fact]
    public async Task Handle_WhenBundleProduct_ReturnsResponseWithVirtualStock()
    {
        Product bundle = ProductTestFactory.CreateBundle(stockQuantity: 0);
        _productRepository.Setup(r => r.GetByIdAsync(bundle.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);

        // Child A: stock=50, ratio=2 -> 25 bundles
        // Child B: stock=30, ratio=3 -> 10 bundles
        // Virtual qty = min(25, 10) = 10
        _productRepository.Setup(r => r.GetBundleComponentDataAsync(
                It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<(int ChildStock, int Ratio)>>
            {
                [bundle.Id] = new List<(int, int)> { (50, 2), (30, 3) }
            });

        Result<ProductResponse> result = await _handler.Handle(
            new GetProductByIdQuery(bundle.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.StockQuantity.Should().Be(10);
        result.Value.Type.Should().Be(ProductType.Bundle);
    }
}
