using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee;

public sealed class ShopeeOrderArrangeServiceTests
{
    private const string TenantId = "tenant-a";
    private static readonly DateTime Now = new(2026, 7, 24, 8, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IShopeeGateway> _gateway = new();
    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IProductComponentRepository> _productComponentRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ShopeeOrderArrangeService _service;

    public ShopeeOrderArrangeServiceTests()
    {
        ShopeeOrderShipmentCompletionService completionService = new(
            _waybillRepository.Object,
            _productRepository.Object,
            new StockReservationService(_productRepository.Object, _productComponentRepository.Object),
            _unitOfWork.Object,
            NullLogger<ShopeeOrderShipmentCompletionService>.Instance);
        _service = new ShopeeOrderArrangeService(_gateway.Object, completionService, _unitOfWork.Object);
    }

    private static ShopeeShopConnection CreateConnection() => ShopeeShopConnection.Create(
        TenantId, 1001, "access-token", "refresh-token",
        Now.AddHours(1), Now.AddDays(30), Now, "linker").Value;

    private static ShopeeOrder CreateReadyToShipOrder()
    {
        ShopeeOrderSnapshot snapshot = new(
            "READY_TO_SHIP", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", null);
        return ShopeeOrder.Create(TenantId, "SN1", "MY", snapshot,
            [new ShopeeOrderItemSnapshot(111, 0, "Item", null, null, 2, Guid.NewGuid())], Now).Value;
    }

    [Fact]
    public async Task ArrangeAsync_Success_RecordsExplicitActor()
    {
        ShopeeOrder order = CreateReadyToShipOrder();
        _gateway
            .Setup(g => g.ShipOrderAsync(1001, "access-token",
                It.IsAny<ShopeeShipOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _gateway
            .Setup(g => g.GetTrackingNumberAsync(1001, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string?>.Success(null));

        Result result = await _service.ArrangeAsync(
            order, CreateConnection(),
            new ShopeeShipOrderRequest("SN1", null, new ShopeeShipOrderDropoff(null)),
            "system:auto-arrange", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.AwaitingTracking);
        order.ShipmentArrangedBy.Should().Be("system:auto-arrange");
    }

    [Fact]
    public async Task ArrangeAsync_ShipOrderFails_MarksShipmentFailed()
    {
        ShopeeOrder order = CreateReadyToShipOrder();
        _gateway
            .Setup(g => g.ShipOrderAsync(1001, "access-token",
                It.IsAny<ShopeeShipOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(new Error("Shopee.ShipRejected", "rejected", ErrorType.Failure)));

        Result result = await _service.ArrangeAsync(
            order, CreateConnection(),
            new ShopeeShipOrderRequest("SN1", null, new ShopeeShipOrderDropoff(null)),
            "system:auto-arrange", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.ShipmentRequestFailed);
        order.Status.Should().Be(ShopeeOrderStatus.ShipmentFailed);
    }
}
