using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.PurchaseOrders.Commands.UpdatePurchaseOrder;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.PurchaseOrders.Commands;

public class UpdatePurchaseOrderCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly UpdatePurchaseOrderCommandHandler _handler;
    private readonly Mock<IPurchaseOrderRepository> _purchaseOrderRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public UpdatePurchaseOrderCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _handler = new UpdatePurchaseOrderCommandHandler(
            _purchaseOrderRepository.Object,
            _tenantContext.Object,
            _unitOfWork.Object);
    }

    private static PurchaseOrder CreatePending() =>
        PurchaseOrder.Create(TenantId, "PO-1", Guid.NewGuid(), "BC-1", "Widget", 3).Value;

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFound()
    {
        _purchaseOrderRepository.Setup(r =>
                r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseOrder?)null);

        Result result = await _handler.Handle(
            new UpdatePurchaseOrderCommand(Guid.NewGuid(), "PO-2", 5), CancellationToken.None);

        result.Error.Code.Should().Be(PurchaseOrderErrors.NotFound.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNumberUsedByAnotherOrder_ReturnsDuplicate()
    {
        PurchaseOrder purchaseOrder = CreatePending();
        _purchaseOrderRepository.Setup(r =>
                r.GetByIdAsync(purchaseOrder.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchaseOrder);
        _purchaseOrderRepository.Setup(r =>
                r.ExistsByNumberAsync("PO-2", purchaseOrder.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result result = await _handler.Handle(
            new UpdatePurchaseOrderCommand(purchaseOrder.Id, "PO-2", 5), CancellationToken.None);

        result.Error.Code.Should().Be(PurchaseOrderErrors.DuplicatePoNumber.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNotPending_ReturnsNotPendingForEdit()
    {
        PurchaseOrder purchaseOrder = CreatePending();
        purchaseOrder.Receive();
        _purchaseOrderRepository.Setup(r =>
                r.GetByIdAsync(purchaseOrder.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchaseOrder);
        _purchaseOrderRepository.Setup(r =>
                r.ExistsByNumberAsync(It.IsAny<string>(), purchaseOrder.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result result = await _handler.Handle(
            new UpdatePurchaseOrderCommand(purchaseOrder.Id, "PO-2", 5), CancellationToken.None);

        result.Error.Code.Should().Be(PurchaseOrderErrors.NotPendingForEdit.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Success_UpdatesAndSaves()
    {
        PurchaseOrder purchaseOrder = CreatePending();
        _purchaseOrderRepository.Setup(r =>
                r.GetByIdAsync(purchaseOrder.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchaseOrder);
        _purchaseOrderRepository.Setup(r =>
                r.ExistsByNumberAsync(It.IsAny<string>(), purchaseOrder.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result result = await _handler.Handle(
            new UpdatePurchaseOrderCommand(purchaseOrder.Id, "PO-EDITED", 12), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        purchaseOrder.PoNumber.Should().Be("PO-EDITED");
        purchaseOrder.Quantity.Should().Be(12);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
