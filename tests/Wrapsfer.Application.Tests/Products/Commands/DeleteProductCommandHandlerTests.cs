using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Products.Commands.DeleteProduct;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Products.Commands;

public class DeleteProductCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly Mock<IProductComponentRepository> _componentRepository = new();
    private readonly DeleteProductCommandHandler _handler;

    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public DeleteProductCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _handler = new DeleteProductCommandHandler(_productRepository.Object, _componentRepository.Object,
            _tenantContext.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WhenProductNotFound_ReturnsNotFoundFailure()
    {
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        Result result = await _handler.Handle(new DeleteProductCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenReferencedAsUnpackTarget_ReturnsFailure()
    {
        Product product = ProductTestFactory.CreateSingle();
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _productRepository.Setup(r =>
                r.IsReferencedAsUnpackTargetAsync(product.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result result = await _handler.Handle(new DeleteProductCommand(product.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.ProductReferencedAsUnpackTarget.Code);
    }

    [Fact]
    public async Task Handle_WhenValid_RemovesProductAndComponentsAndReturnsSuccess()
    {
        Product product = ProductTestFactory.CreateSingle();
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _productRepository.Setup(r =>
                r.IsReferencedAsUnpackTargetAsync(product.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _componentRepository.Setup(r => r.GetByChildIdAsync(product.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductComponent>());

        Result result = await _handler.Handle(new DeleteProductCommand(product.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _productRepository.Verify(r => r.Remove(product), Times.Once);
        _componentRepository.Verify(
            r => r.RemoveAllByParentIdAsync(product.Id, TenantId, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
