using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.PurchaseOrders.Commands.DeletePurchaseOrder;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.PurchaseOrders.Commands;

public class DeletePurchaseOrderCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly DeletePurchaseOrderCommandHandler _handler;
    private readonly Mock<IPurchaseOrderRepository> _purchaseOrderRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public DeletePurchaseOrderCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _handler = new DeletePurchaseOrderCommandHandler(
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
            new DeletePurchaseOrderCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error.Code.Should().Be(PurchaseOrderErrors.NotFound.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNotPending_ReturnsNotPendingForDelete()
    {
        PurchaseOrder purchaseOrder = CreatePending();
        purchaseOrder.Receive();
        _purchaseOrderRepository.Setup(r =>
                r.GetByIdAsync(purchaseOrder.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchaseOrder);

        Result result = await _handler.Handle(
            new DeletePurchaseOrderCommand(purchaseOrder.Id), CancellationToken.None);

        result.Error.Code.Should().Be(PurchaseOrderErrors.NotPendingForDelete.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Success_MarksDeletedAndSaves()
    {
        PurchaseOrder purchaseOrder = CreatePending();
        _purchaseOrderRepository.Setup(r =>
                r.GetByIdAsync(purchaseOrder.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchaseOrder);

        Result result = await _handler.Handle(
            new DeletePurchaseOrderCommand(purchaseOrder.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        purchaseOrder.IsDeleted.Should().BeTrue();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
