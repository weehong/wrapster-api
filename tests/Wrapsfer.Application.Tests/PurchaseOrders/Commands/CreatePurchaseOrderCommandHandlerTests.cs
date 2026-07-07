using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.PurchaseOrders.Commands.CreatePurchaseOrder;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.PurchaseOrders.Commands;

public class CreatePurchaseOrderCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly CreatePurchaseOrderCommandHandler _handler;
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IPurchaseOrderRepository> _purchaseOrderRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public CreatePurchaseOrderCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _handler = new CreatePurchaseOrderCommandHandler(
            _purchaseOrderRepository.Object,
            _productRepository.Object,
            _tenantContext.Object,
            _unitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WhenNumberAlreadyExists_ReturnsDuplicate()
    {
        _purchaseOrderRepository.Setup(r =>
                r.ExistsByNumberAsync("PO-1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result<Guid> result = await _handler.Handle(
            new CreatePurchaseOrderCommand("PO-1", Guid.NewGuid(), 3), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PurchaseOrderErrors.DuplicatePoNumber.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProductNotFound_ReturnsProductNotFound()
    {
        _purchaseOrderRepository.Setup(r =>
                r.ExistsByNumberAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _productRepository.Setup(r =>
                r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        Result<Guid> result = await _handler.Handle(
            new CreatePurchaseOrderCommand("PO-1", Guid.NewGuid(), 3), CancellationToken.None);

        result.Error.Code.Should().Be(PurchaseOrderErrors.ProductNotFound.Code);
        _purchaseOrderRepository.Verify(r => r.Add(It.IsAny<PurchaseOrder>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProductInactive_ReturnsProductInactive()
    {
        Product product = ProductTestFactory.CreateSingle(tenantId: TenantId, barcode: "BC-1");
        product.Deactivate(DateTime.UtcNow);

        _purchaseOrderRepository.Setup(r =>
                r.ExistsByNumberAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _productRepository.Setup(r =>
                r.GetByIdAsync(product.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        Result<Guid> result = await _handler.Handle(
            new CreatePurchaseOrderCommand("PO-1", product.Id, 3), CancellationToken.None);

        result.Error.Code.Should().Be(PurchaseOrderErrors.ProductInactive.Code);
        _purchaseOrderRepository.Verify(r => r.Add(It.IsAny<PurchaseOrder>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProductIsBundle_ReturnsNotStockable()
    {
        Product product = ProductTestFactory.CreateBundle(tenantId: TenantId, barcode: "BC-1");

        _purchaseOrderRepository.Setup(r =>
                r.ExistsByNumberAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _productRepository.Setup(r =>
                r.GetByIdAsync(product.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        Result<Guid> result = await _handler.Handle(
            new CreatePurchaseOrderCommand("PO-1", product.Id, 3), CancellationToken.None);

        result.Error.Code.Should().Be(PurchaseOrderErrors.ProductNotStockable.Code);
        _purchaseOrderRepository.Verify(r => r.Add(It.IsAny<PurchaseOrder>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Success_AddsPurchaseOrderAndSaves()
    {
        Product product = ProductTestFactory.CreateSingle(tenantId: TenantId, barcode: "BC-1", name: "Widget");

        _purchaseOrderRepository.Setup(r =>
                r.ExistsByNumberAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _productRepository.Setup(r =>
                r.GetByIdAsync(product.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        Result<Guid> result = await _handler.Handle(
            new CreatePurchaseOrderCommand("PO-1", product.Id, 7), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _purchaseOrderRepository.Verify(r => r.Add(It.Is<PurchaseOrder>(p =>
            p.ProductId == product.Id &&
            p.ProductBarcode == "BC-1" &&
            p.ProductName == "Widget" &&
            p.Quantity == 7)), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
