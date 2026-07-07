using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.PurchaseOrders.Commands.RejectPurchaseOrder;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.PurchaseOrders.Commands;

public class RejectPurchaseOrderCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly RejectPurchaseOrderCommandHandler _handler;
    private readonly Mock<IPurchaseOrderRepository> _purchaseOrderRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public RejectPurchaseOrderCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _handler = new RejectPurchaseOrderCommandHandler(
            _purchaseOrderRepository.Object,
            _tenantContext.Object,
            _unitOfWork.Object);
    }

    private static PurchaseOrder CreatePending() =>
        PurchaseOrder.Create(TenantId, "PO-1", Guid.NewGuid(), "BC-1", "Widget", 5).Value;

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFound()
    {
        _purchaseOrderRepository.Setup(r =>
                r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseOrder?)null);

        Result result = await _handler.Handle(
            new RejectPurchaseOrderCommand(Guid.NewGuid(), "no stock"), CancellationToken.None);

        result.Error.Code.Should().Be(PurchaseOrderErrors.NotFound.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Success_RejectsAndSaves()
    {
        PurchaseOrder purchaseOrder = CreatePending();

        _purchaseOrderRepository.Setup(r =>
                r.GetByIdAsync(purchaseOrder.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(purchaseOrder);

        Result result = await _handler.Handle(
            new RejectPurchaseOrderCommand(purchaseOrder.Id, "Supplier cancelled"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        purchaseOrder.Status.Should().Be(PurchaseOrderStatus.Rejected);
        purchaseOrder.RejectionReason.Should().Be("Supplier cancelled");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
