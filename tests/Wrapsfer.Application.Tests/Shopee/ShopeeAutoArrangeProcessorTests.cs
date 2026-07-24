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
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee;

public sealed class ShopeeAutoArrangeProcessorTests
{
    private const string TenantId = "tenant-a";
    private const int BatchSize = 50;
    private static readonly DateTime Now = new(2026, 7, 24, 8, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IFulfillmentDelegationRepository> _delegationRepository = new();
    private readonly Mock<IShopeeShopConnectionRepository> _connectionRepository = new();
    private readonly Mock<IShopeeOrderRepository> _orderRepository = new();
    private readonly Mock<IShopeeProductLinkRepository> _linkRepository = new();
    private readonly Mock<IShopeeGateway> _gateway = new();
    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IProductComponentRepository> _productComponentRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ShopeeAutoArrangeProcessor _processor;

    public ShopeeAutoArrangeProcessorTests()
    {
        StockReservationService stockReservationService =
            new(_productRepository.Object, _productComponentRepository.Object);
        ShopeeOrderCancellationService cancellationService = new(
            _waybillRepository.Object, stockReservationService,
            NullLogger<ShopeeOrderCancellationService>.Instance);
        ShopeeOrderIngestionService ingestionService = new(
            _gateway.Object, _orderRepository.Object, _linkRepository.Object,
            _delegationRepository.Object, _productRepository.Object,
            cancellationService, _unitOfWork.Object,
            NullLogger<ShopeeOrderIngestionService>.Instance);
        ShopeeOrderShipmentCompletionService completionService = new(
            _waybillRepository.Object, _productRepository.Object, stockReservationService,
            _unitOfWork.Object, NullLogger<ShopeeOrderShipmentCompletionService>.Instance);
        ShopeeOrderArrangeService arrangeService = new(
            _gateway.Object, completionService, _unitOfWork.Object);
        ShopeeConnectionTokenRefresher tokenRefresher = new(
            _gateway.Object, _unitOfWork.Object,
            NullLogger<ShopeeConnectionTokenRefresher>.Instance);

        _processor = new ShopeeAutoArrangeProcessor(
            _delegationRepository.Object,
            _connectionRepository.Object,
            _orderRepository.Object,
            ingestionService,
            arrangeService,
            tokenRefresher,
            _gateway.Object,
            _unitOfWork.Object,
            NullLogger<ShopeeAutoArrangeProcessor>.Instance);
    }

    private static FulfillmentDelegation CreateActiveDelegation()
    {
        FulfillmentDelegation delegation =
            FulfillmentDelegation.Request(TenantId, FulfillmentShippingMethod.Dropoff).Value;
        delegation.Accept();
        return delegation;
    }

    private static ShopeeShopConnection CreateConnection() => ShopeeShopConnection.Create(
        TenantId, 1001, "access-token", "refresh-token",
        Now.AddHours(1), Now.AddDays(30), Now, "linker").Value;

    private static ShopeeOrder CreateOrder(string orderSn, DateTime? shipBy)
    {
        ShopeeOrderSnapshot snapshot = new(
            "READY_TO_SHIP", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", shipBy);
        return ShopeeOrder.Create(TenantId, orderSn, "MY", snapshot,
            [new ShopeeOrderItemSnapshot(111, 0, "Item", null, null, 2, Guid.NewGuid())], Now).Value;
    }

    private void SetupTenant(params ShopeeOrder[] readyOrders)
    {
        _delegationRepository
            .Setup(r => r.ListAsync(FulfillmentDelegationStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FulfillmentDelegation> { CreateActiveDelegation() });
        _connectionRepository
            .Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConnection());
        _orderRepository
            .Setup(r => r.ListAsync(TenantId, ShopeeOrderStatus.NeedsLinking, null, 1, BatchSize,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ShopeeOrder>(), 0));
        _orderRepository
            .Setup(r => r.ListAsync(TenantId, ShopeeOrderStatus.ReadyToShip, null, 1, BatchSize,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((readyOrders.ToList(), readyOrders.Length));
        foreach (ShopeeOrder order in readyOrders)
        {
            _orderRepository
                .Setup(r => r.GetByIdAsync(order.Id, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);
        }
    }

    [Fact]
    public async Task RunAsync_NoActiveDelegations_DoesNothing()
    {
        _delegationRepository
            .Setup(r => r.ListAsync(FulfillmentDelegationStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FulfillmentDelegation>());

        ShopeeAutoArrangeRunSummary summary = await _processor.RunAsync(BatchSize, CancellationToken.None);

        summary.TenantsExamined.Should().Be(0);
        _connectionRepository.Verify(
            r => r.GetByTenantIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_TenantWithoutConnection_IsSkipped()
    {
        _delegationRepository
            .Setup(r => r.ListAsync(FulfillmentDelegationStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FulfillmentDelegation> { CreateActiveDelegation() });
        _connectionRepository
            .Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopeeShopConnection?)null);

        ShopeeAutoArrangeRunSummary summary = await _processor.RunAsync(BatchSize, CancellationToken.None);

        summary.TenantsExamined.Should().Be(1);
        summary.OrdersArranged.Should().Be(0);
        _orderRepository.Verify(r => r.ListAsync(It.IsAny<string>(), It.IsAny<ShopeeOrderStatus?>(),
            It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_ReadyOrder_ArrangesWithSystemActor()
    {
        ShopeeOrder order = CreateOrder("SN1", shipBy: Now.AddDays(2));
        SetupTenant(order);
        _gateway
            .Setup(g => g.GetShippingParameterAsync(1001, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeShippingParameter>.Success(
                new ShopeeShippingParameter(false, true, [], [])));
        _gateway
            .Setup(g => g.ShipOrderAsync(1001, "access-token",
                It.Is<ShopeeShipOrderRequest>(r => r.OrderSn == "SN1" && r.Dropoff != null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _gateway
            .Setup(g => g.GetTrackingNumberAsync(1001, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string?>.Success(null));

        ShopeeAutoArrangeRunSummary summary = await _processor.RunAsync(BatchSize, CancellationToken.None);

        summary.OrdersArranged.Should().Be(1);
        order.Status.Should().Be(ShopeeOrderStatus.AwaitingTracking);
        order.ShipmentArrangedBy.Should().Be(ShopeeAutoArrangeProcessor.SystemActor);
    }

    [Fact]
    public async Task RunAsync_OrderPastShipBy_IsSkippedWithoutGatewayCalls()
    {
        ShopeeOrder order = CreateOrder("SN1", shipBy: Now.AddDays(-1));
        SetupTenant(order);

        ShopeeAutoArrangeRunSummary summary = await _processor.RunAsync(BatchSize, CancellationToken.None);

        summary.OrdersSkipped.Should().Be(1);
        summary.OrdersArranged.Should().Be(0);
        order.Status.Should().Be(ShopeeOrderStatus.ReadyToShip);
        _gateway.Verify(g => g.GetShippingParameterAsync(It.IsAny<long>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_ParamFetchFails_LeavesOrderReadyToShip()
    {
        ShopeeOrder order = CreateOrder("SN1", shipBy: Now.AddDays(2));
        SetupTenant(order);
        _gateway
            .Setup(g => g.GetShippingParameterAsync(1001, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeShippingParameter>.Failure(
                new Error("Shopee.ServerError", "boom", ErrorType.Failure)));

        ShopeeAutoArrangeRunSummary summary = await _processor.RunAsync(BatchSize, CancellationToken.None);

        summary.OrdersFailed.Should().Be(0);
        order.Status.Should().Be(ShopeeOrderStatus.ReadyToShip);
        _gateway.Verify(g => g.ShipOrderAsync(It.IsAny<long>(), It.IsAny<string>(),
            It.IsAny<ShopeeShipOrderRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_NoUsableShippingOption_MarksShipmentFailed()
    {
        ShopeeOrder order = CreateOrder("SN1", shipBy: Now.AddDays(2));
        SetupTenant(order);
        _gateway
            .Setup(g => g.GetShippingParameterAsync(1001, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeShippingParameter>.Success(
                new ShopeeShippingParameter(false, false, [], [])));

        ShopeeAutoArrangeRunSummary summary = await _processor.RunAsync(BatchSize, CancellationToken.None);

        summary.OrdersFailed.Should().Be(1);
        order.Status.Should().Be(ShopeeOrderStatus.ShipmentFailed);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunAsync_NeedsLinkingOrder_GoesThroughIngestion()
    {
        ShopeeOrder needsLinking = ShopeeOrder.Create(TenantId, "SN2", "MY",
            new ShopeeOrderSnapshot("READY_TO_SHIP", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX",
                Now.AddDays(2)),
            [new ShopeeOrderItemSnapshot(111, 0, "Item", null, null, 2, null)], Now).Value;
        SetupTenant();
        _orderRepository
            .Setup(r => r.ListAsync(TenantId, ShopeeOrderStatus.NeedsLinking, null, 1, BatchSize,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ShopeeOrder> { needsLinking }, 1));
        _gateway
            .Setup(g => g.GetOrderDetailAsync(1001, "access-token", "SN2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderDetail>.Failure(
                new Error("Shopee.ServerError", "boom", ErrorType.Failure)));

        ShopeeAutoArrangeRunSummary summary = await _processor.RunAsync(BatchSize, CancellationToken.None);

        summary.RelinkAttempts.Should().Be(1);
        _gateway.Verify(g => g.GetOrderDetailAsync(1001, "access-token", "SN2",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
