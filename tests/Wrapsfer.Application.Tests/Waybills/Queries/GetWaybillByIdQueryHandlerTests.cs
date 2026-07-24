using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Waybills.Queries.GetWaybillById;
using Wrapsfer.Application.Waybills.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Waybills.Queries;

public class GetWaybillByIdQueryHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly GetWaybillByIdQueryHandler _handler;

    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IShopeeOrderRepository> _shopeeOrderRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();

    public GetWaybillByIdQueryHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _handler = new GetWaybillByIdQueryHandler(
            _waybillRepository.Object,
            _productRepository.Object,
            _shopeeOrderRepository.Object,
            _tenantContext.Object);
    }

    private static Waybill CreateWaybill(string tenantId, string number)
    {
        return Waybill.Create(tenantId, new DateOnly(2026, 5, 1), number).Value;
    }

    [Fact]
    public async Task Handle_WhenWaybillNotFound_ReturnsNotFound()
    {
        _waybillRepository.Setup(r => r.GetByIdWithItemsAsync(
                It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Waybill?)null);

        Result<WaybillResponse> result = await _handler.Handle(
            new GetWaybillByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(WaybillErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenWaybillLinkedToShopeeOrder_ReturnsShopeeOrderId()
    {
        Waybill waybill = CreateWaybill(TenantId, "WB-1");
        Guid shopeeOrderId = Guid.NewGuid();

        _waybillRepository.Setup(r => r.GetByIdWithItemsAsync(
                waybill.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybill);
        _shopeeOrderRepository.Setup(r => r.FindIdByWaybillIdAsync(
                waybill.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shopeeOrderId);

        Result<WaybillResponse> result = await _handler.Handle(
            new GetWaybillByIdQuery(waybill.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShopeeOrderId.Should().Be(shopeeOrderId);
    }

    [Fact]
    public async Task Handle_WhenManualWaybill_ReturnsNullShopeeOrderId()
    {
        Waybill waybill = CreateWaybill(TenantId, "WB-1");

        _waybillRepository.Setup(r => r.GetByIdWithItemsAsync(
                waybill.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybill);
        _shopeeOrderRepository.Setup(r => r.FindIdByWaybillIdAsync(
                waybill.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        Result<WaybillResponse> result = await _handler.Handle(
            new GetWaybillByIdQuery(waybill.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShopeeOrderId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_LooksUpShopeeOrderWithinCurrentTenantOnly()
    {
        Waybill waybill = CreateWaybill(TenantId, "WB-1");

        _waybillRepository.Setup(r => r.GetByIdWithItemsAsync(
                waybill.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybill);

        await _handler.Handle(new GetWaybillByIdQuery(waybill.Id), CancellationToken.None);

        _shopeeOrderRepository.Verify(r => r.FindIdByWaybillIdAsync(
            waybill.Id, TenantId, It.IsAny<CancellationToken>()), Times.Once);
        _shopeeOrderRepository.Verify(r => r.FindIdByWaybillIdAsync(
            It.IsAny<Guid>(), It.Is<string>(t => t != TenantId), It.IsAny<CancellationToken>()), Times.Never);
    }
}
