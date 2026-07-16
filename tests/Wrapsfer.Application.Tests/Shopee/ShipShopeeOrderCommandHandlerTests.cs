using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Commands.ShipShopeeOrder;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee;

public sealed class ShipShopeeOrderCommandHandlerTests
{
    private const string TenantId = "tenant-a";
    private const string OrderSn = "SN1";
    private const string Username = "user-a";
    private static readonly DateTime Now = new(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OrderId = Guid.NewGuid();
    private readonly Mock<IShopeeOrderRepository> _orderRepository = new();
    private readonly Mock<IShopeeShopConnectionRepository> _connectionRepository = new();
    private readonly Mock<IShopeeGateway> _gateway = new();
    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IProductComponentRepository> _productComponentRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ShopeeConnectionTokenRefresher _tokenRefresher;
    private readonly ShopeeOrderShipmentCompletionService _completionService;
    private readonly ShipShopeeOrderCommandHandler _handler;

    public ShipShopeeOrderCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.Username).Returns(Username);
        _tokenRefresher = new ShopeeConnectionTokenRefresher(
            _gateway.Object, _unitOfWork.Object, NullLogger<ShopeeConnectionTokenRefresher>.Instance);
        _completionService = new ShopeeOrderShipmentCompletionService(
            _waybillRepository.Object,
            _productRepository.Object,
            new StockReservationService(_productRepository.Object, _productComponentRepository.Object),
            _unitOfWork.Object,
            NullLogger<ShopeeOrderShipmentCompletionService>.Instance);
        _handler = new ShipShopeeOrderCommandHandler(
            _orderRepository.Object,
            _connectionRepository.Object,
            _gateway.Object,
            _tokenRefresher,
            _completionService,
            _tenantContext.Object,
            _unitOfWork.Object);
    }

    [Fact]
    public async Task Handle_DropoffHappyPathWithoutImmediateTracking_EndsAwaitingTracking()
    {
        ShopeeOrder order = CreateOrder(Guid.NewGuid());
        ShopeeShopConnection connection = CreateConnection();
        SetupOrderAndConnection(order, connection);
        _gateway
            .Setup(g => g.ShipOrderAsync(
                connection.ShopId, connection.AccessToken, It.IsAny<ShopeeShipOrderRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _gateway
            .Setup(g => g.GetTrackingNumberAsync(
                connection.ShopId, connection.AccessToken, OrderSn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string?>.Success(null));

        Result<ShopeeOrderResponse> result = await _handler.Handle(
            new ShipShopeeOrderCommand(TenantId, OrderId, "dropoff", null, null, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.AwaitingTracking);
        result.Value.Status.Should().Be(ShopeeOrderStatus.AwaitingTracking.ToString());
        _waybillRepository.Verify(r => r.Add(It.IsAny<Waybill>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ImmediateTrackingAvailable_CompletesShipmentAndReturnsShipped()
    {
        Product product = ProductTestFactory.CreateSingle(TenantId, stockQuantity: 10);
        ShopeeOrder order = CreateOrder(product.Id);
        ShopeeShopConnection connection = CreateConnection();
        SetupOrderAndConnection(order, connection);
        _gateway
            .Setup(g => g.ShipOrderAsync(
                connection.ShopId, connection.AccessToken, It.IsAny<ShopeeShipOrderRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _gateway
            .Setup(g => g.GetTrackingNumberAsync(
                connection.ShopId, connection.AccessToken, OrderSn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string?>.Success("TRACK1"));
        _waybillRepository
            .Setup(r => r.ExistsByNumberAsync("TRACK1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _productRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        Result<ShopeeOrderResponse> result = await _handler.Handle(
            new ShipShopeeOrderCommand(TenantId, OrderId, "dropoff", null, null, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.Shipped);
        result.Value.Status.Should().Be(ShopeeOrderStatus.Shipped.ToString());
        result.Value.TrackingNumber.Should().Be("TRACK1");
        _waybillRepository.Verify(r => r.Add(It.IsAny<Waybill>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShipOrderAsyncFails_MarksShipmentFailedAndReturnsShipmentRequestFailed()
    {
        ShopeeOrder order = CreateOrder(Guid.NewGuid());
        ShopeeShopConnection connection = CreateConnection();
        SetupOrderAndConnection(order, connection);
        Error gatewayError = new("Shopee.Rejected", "Shopee rejected the request", ErrorType.Failure);
        _gateway
            .Setup(g => g.ShipOrderAsync(
                connection.ShopId, connection.AccessToken, It.IsAny<ShopeeShipOrderRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(gatewayError));

        Result<ShopeeOrderResponse> result = await _handler.Handle(
            new ShipShopeeOrderCommand(TenantId, OrderId, "dropoff", null, null, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.ShipmentRequestFailed);
        order.Status.Should().Be(ShopeeOrderStatus.ShipmentFailed);
        order.LastShipError.Should().Be(gatewayError.Description);
        _gateway.Verify(g => g.GetTrackingNumberAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OrderNeedsLinking_ReturnsNotReadyToShipWithoutCallingGateway()
    {
        ShopeeOrder order = CreateOrder(productId: null);
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        Result<ShopeeOrderResponse> result = await _handler.Handle(
            new ShipShopeeOrderCommand(TenantId, OrderId, "dropoff", null, null, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.NotReadyToShip);
        _gateway.Verify(g => g.ShipOrderAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<ShopeeShipOrderRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OrderInCancel_ReturnsCancellationRequestedWithoutCallingGateway()
    {
        ShopeeOrder order = CreateOrder(Guid.NewGuid(), shopeeStatus: "IN_CANCEL");
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        Result<ShopeeOrderResponse> result = await _handler.Handle(
            new ShipShopeeOrderCommand(TenantId, OrderId, "dropoff", null, null, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.CancellationRequested);
        _gateway.Verify(g => g.ShipOrderAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<ShopeeShipOrderRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _connectionRepository.Verify(r => r.GetByTenantIdAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetupOrderAndConnection(ShopeeOrder order, ShopeeShopConnection connection)
    {
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _connectionRepository
            .Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);
    }

    private static ShopeeOrder CreateOrder(
        Guid? productId, string shopeeStatus = "READY_TO_SHIP")
    {
        ShopeeOrderSnapshot snapshot = new(
            shopeeStatus, "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", null);
        ShopeeOrder order = ShopeeOrder.Create(
            TenantId, OrderSn, "MY", snapshot,
            [new ShopeeOrderItemSnapshot(1, 0, "n", null, null, 2, productId)], Now).Value;
        return order;
    }

    private static ShopeeShopConnection CreateConnection() =>
        ShopeeShopConnection.Create(
            TenantId,
            123456,
            "access-token",
            "refresh-token",
            Now.AddHours(4),
            Now.AddDays(30),
            Now,
            null).Value;
}
