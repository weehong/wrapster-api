using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Products;
using Wrapsfer.Application.PurchaseOrders.Commands.ReceivePurchaseOrder;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.PurchaseOrders.Commands;

public class ReceivePurchaseOrderCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly ReceivePurchaseOrderCommandHandler _handler;
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IPurchaseOrderRepository> _purchaseOrderRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<ITenantSettingsRepository> _tenantSettingsRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public ReceivePurchaseOrderCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _handler = new ReceivePurchaseOrderCommandHandler(
            _purchaseOrderRepository.Object,
            _productRepository.Object,
            _tenantSettingsRepository.Object,
            _tenantContext.Object,
            _unitOfWork.Object,
            Options.Create(new ProductSettings()));
    }

    private static PurchaseOrder CreatePendingFor(Product product, int quantity = 5) =>
        PurchaseOrder.Create(TenantId, "PO-1", product.Id, product.Barcode, product.Name, quantity).Value;

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFound()
    {
        _purchaseOrderRepository.Setup(r =>
                r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseOrder?)null);

        Result result = await _handler.Handle(
            new ReceivePurchaseOrderCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error.Code.Should().Be(PurchaseOrderErrors.NotFound.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Success_RechargesStockAndSaves()
    {
        Product product = ProductTestFactory.CreateSingle(tenantId: TenantId, barcode: "BC-1", stockQuantity: 10);
        PurchaseOrder purchaseOrder = CreatePendingFor(product, quantity: 8);

        _purchaseOrderRepository.Setup(r =>
                r.GetByIdAsync(purchaseOrder.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchaseOrder);
        _productRepository.Setup(r =>
                r.GetByIdAsync(product.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        Result result = await _handler.Handle(
            new ReceivePurchaseOrderCommand(purchaseOrder.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        purchaseOrder.Status.Should().Be(Domain.Enums.PurchaseOrderStatus.Received);
        product.StockQuantity.Should().Be(18);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlreadyReceived_ReturnsInvalidTransitionAndDoesNotSave()
    {
        Product product = ProductTestFactory.CreateSingle(tenantId: TenantId, barcode: "BC-1", stockQuantity: 10);
        PurchaseOrder purchaseOrder = CreatePendingFor(product);
        purchaseOrder.Receive();

        _purchaseOrderRepository.Setup(r =>
                r.GetByIdAsync(purchaseOrder.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchaseOrder);

        Result result = await _handler.Handle(
            new ReceivePurchaseOrderCommand(purchaseOrder.Id), CancellationToken.None);

        result.Error.Code.Should().Be(PurchaseOrderErrors.InvalidStatusTransition.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
