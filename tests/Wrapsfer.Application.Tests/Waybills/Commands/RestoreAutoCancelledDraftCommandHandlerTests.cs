using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Application.Waybills.Commands.RestoreAutoCancelledDraft;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Waybills.Commands;

public class RestoreAutoCancelledDraftCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly Mock<IProductComponentRepository> _componentRepository = new();
    private readonly RestoreAutoCancelledDraftCommandHandler _handler;
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IWaybillRepository> _waybillRepository = new();

    public RestoreAutoCancelledDraftCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        StockReservationService stockService =
            new(_productRepository.Object, _componentRepository.Object);
        _handler = new RestoreAutoCancelledDraftCommandHandler(
            _waybillRepository.Object,
            stockService,
            _tenantContext.Object,
            _unitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFound()
    {
        _waybillRepository.Setup(r => r.GetByIdWithItemsAsync(It.IsAny<Guid>(), TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Waybill?)null);

        Result result = await _handler.Handle(
            new RestoreAutoCancelledDraftCommand(Guid.NewGuid()),
            CancellationToken.None);

        result.Error.Code.Should().Be(WaybillErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenAutoCancelledStaleDraft_ReservesItemsAndRestoresDraft()
    {
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 10);
        Waybill waybill = CreateAutoCancelledWaybill(product, 3);

        SetupWaybill(waybill);
        SetupProducts(product);

        Result result = await _handler.Handle(
            new RestoreAutoCancelledDraftCommand(waybill.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        waybill.Status.Should().Be(WaybillStatus.Draft);
        waybill.CancellationReason.Should().BeNull();
        waybill.CancelledAt.Should().BeNull();
        product.ReservedQuantity.Should().Be(3);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenManuallyCancelled_ReturnsCannotRestore()
    {
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 10);
        Waybill waybill = Waybill.Create(TenantId, new DateOnly(2026, 4, 13), "WB-1").Value;
        waybill.AddOrIncrementItem(product.Id, product.Barcode, 3);
        waybill.Cancel("customer change of mind");

        SetupWaybill(waybill);

        Result result = await _handler.Handle(
            new RestoreAutoCancelledDraftCommand(waybill.Id),
            CancellationToken.None);

        result.Error.Code.Should().Be(WaybillErrors.CannotRestoreNonAutoCancelledDraft.Code);
        waybill.Status.Should().Be(WaybillStatus.Cancelled);
        product.ReservedQuantity.Should().Be(0);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenStockCannotBeReserved_LeavesWaybillCancelled()
    {
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 2);
        Waybill waybill = CreateAutoCancelledWaybill(product, 3);

        SetupWaybill(waybill);
        SetupProducts(product);

        Result result = await _handler.Handle(
            new RestoreAutoCancelledDraftCommand(waybill.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        waybill.Status.Should().Be(WaybillStatus.Cancelled);
        waybill.CancellationReason.Should().Be(Waybill.AutoCancelledStaleDraftReason);
        product.ReservedQuantity.Should().Be(0);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Waybill CreateAutoCancelledWaybill(Product product, int quantity)
    {
        Waybill waybill = Waybill.Create(TenantId, new DateOnly(2026, 4, 13), "WB-1").Value;
        waybill.AddOrIncrementItem(product.Id, product.Barcode, quantity);
        waybill.Cancel(Waybill.AutoCancelledStaleDraftReason);
        return waybill;
    }

    private void SetupWaybill(Waybill waybill)
    {
        _waybillRepository.Setup(r => r.GetByIdWithItemsAsync(waybill.Id, TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybill);
    }

    private void SetupProducts(params Product[] products)
    {
        _productRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);
    }
}
