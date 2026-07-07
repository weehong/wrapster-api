using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Products.Commands.SetProductActive;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Products.Commands;

public class SetProductActiveCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly SetProductActiveCommandHandler _handler;
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public SetProductActiveCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _handler = new SetProductActiveCommandHandler(_productRepository.Object, _tenantContext.Object,
            _unitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WhenProductNotFound_ReturnsNotFound()
    {
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        Result result = await _handler.Handle(new SetProductActiveCommand(Guid.NewGuid(), false),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.NotFound.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DeactivatesActiveProductAndSaves()
    {
        Product product = ProductTestFactory.CreateSingle();
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        Result result = await _handler.Handle(new SetProductActiveCommand(product.Id, false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.IsActive.Should().BeFalse();
        product.DeactivatedAt.Should().NotBeNull();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReactivatesInactiveProductAndSaves()
    {
        Product product = ProductTestFactory.CreateSingle();
        product.Deactivate(DateTime.UtcNow);
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        Result result = await _handler.Handle(new SetProductActiveCommand(product.Id, true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.IsActive.Should().BeTrue();
        product.DeactivatedAt.Should().BeNull();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DeactivatingAlreadyInactive_ReturnsConflict()
    {
        Product product = ProductTestFactory.CreateSingle();
        product.Deactivate(DateTime.UtcNow);
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        Result result = await _handler.Handle(new SetProductActiveCommand(product.Id, false),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.AlreadyInactive.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
