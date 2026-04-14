using Wrapster.Application.Abstractions;
using Wrapster.Application.Tests.Helpers;
using Wrapster.Application.Waybills.Commands.DeleteWaybill;
using Wrapster.Application.Waybills.Services;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Tests.Waybills.Commands;

public class DeleteWaybillCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly Mock<IProductComponentRepository> _componentRepository = new();
    private readonly DeleteWaybillCommandHandler _handler;
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly Mock<IWaybillRepository> _waybillRepository = new();

    public DeleteWaybillCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        StockReservationService stockService =
            new(_productRepository.Object, _componentRepository.Object);
        _handler = new DeleteWaybillCommandHandler(_waybillRepository.Object, stockService,
            _tenantContext.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFound()
    {
        _waybillRepository.Setup(r => r.GetByIdWithItemsAsync(It.IsAny<Guid>(), TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Waybill?)null);

        Result result = await _handler.Handle(new DeleteWaybillCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error.Code.Should().Be(WaybillErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenNotDraft_ReturnsCannotDeleteNonDraft()
    {
        Waybill waybill = Waybill.Create(TenantId, new DateOnly(2026, 4, 13), "WB-1").Value;
        waybill.AddOrIncrementItem(Guid.NewGuid(), "BC-1", 1);
        waybill.MarkPacked();

        _waybillRepository.Setup(r => r.GetByIdWithItemsAsync(waybill.Id, TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybill);

        Result result = await _handler.Handle(new DeleteWaybillCommand(waybill.Id), CancellationToken.None);

        result.Error.Code.Should().Be(WaybillErrors.CannotDeleteNonDraft.Code);
        _waybillRepository.Verify(r => r.Remove(It.IsAny<Waybill>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DraftWithItems_ReleasesReservationsAndRemoves()
    {
        Product product = ProductTestFactory.CreateSingle(stockQuantity: 10);
        product.Reserve(3);

        Waybill waybill = Waybill.Create(TenantId, new DateOnly(2026, 4, 13), "WB-1").Value;
        waybill.AddOrIncrementItem(product.Id, product.Barcode, 3);

        _waybillRepository.Setup(r => r.GetByIdWithItemsAsync(waybill.Id, TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybill);
        _productRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        Result result = await _handler.Handle(new DeleteWaybillCommand(waybill.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        product.ReservedQuantity.Should().Be(0);
        _waybillRepository.Verify(r => r.Remove(waybill), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
