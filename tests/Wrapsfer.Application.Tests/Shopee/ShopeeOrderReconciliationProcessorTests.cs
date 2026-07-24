using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions.Shopee;
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

public sealed class ShopeeOrderReconciliationProcessorTests
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";
    private const long ShopIdA = 1001;
    private const long ShopIdB = 1002;
    private static readonly DateTime Now = new(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IShopeeShopConnectionRepository> _connectionRepository = new();
    private readonly Mock<IShopeeOrderRepository> _orderRepository = new();
    private readonly Mock<IShopeeGateway> _gateway = new();
    private readonly Mock<IShopeeProductLinkRepository> _linkRepository = new();
    private readonly Mock<IFulfillmentDelegationRepository> _delegationRepository = new();
    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IProductComponentRepository> _productComponentRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public ShopeeOrderReconciliationProcessorTests()
    {
        _linkRepository
            .Setup(r => r.ListByTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ShopeeProductLink>());
        _orderRepository
            .Setup(r => r.ListAwaitingTrackingAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ShopeeOrder>());
    }

    private ShopeeOrderReconciliationProcessor CreateProcessor()
    {
        StockReservationService stockReservationService =
            new(_productRepository.Object, _productComponentRepository.Object);
        ShopeeOrderCancellationService cancellationService = new(
            _waybillRepository.Object,
            stockReservationService,
            NullLogger<ShopeeOrderCancellationService>.Instance);
        ShopeeOrderIngestionService ingestionService = new(
            _gateway.Object,
            _orderRepository.Object,
            _linkRepository.Object,
            _delegationRepository.Object,
            _productRepository.Object,
            cancellationService,
            _unitOfWork.Object,
            NullLogger<ShopeeOrderIngestionService>.Instance);
        ShopeeOrderShipmentCompletionService completionService = new(
            _waybillRepository.Object,
            _productRepository.Object,
            stockReservationService,
            _unitOfWork.Object,
            NullLogger<ShopeeOrderShipmentCompletionService>.Instance);
        ShopeeConnectionTokenRefresher tokenRefresher =
            new(_gateway.Object, _unitOfWork.Object, NullLogger<ShopeeConnectionTokenRefresher>.Instance);

        return new ShopeeOrderReconciliationProcessor(
            _connectionRepository.Object,
            _orderRepository.Object,
            _gateway.Object,
            ingestionService,
            completionService,
            tokenRefresher,
            _unitOfWork.Object,
            NullLogger<ShopeeOrderReconciliationProcessor>.Instance);
    }

    private ShopeeShopConnection CreateConnection(string tenantId, long shopId, string accessToken)
    {
        ShopeeShopConnection connection = ShopeeShopConnection.Create(
            tenantId, shopId, accessToken, $"{accessToken}-refresh",
            Now.AddDays(1), Now.AddDays(30), Now, "linker").Value;

        // Mirrors the per-iteration fresh fetch the sweep performs: in these mock-based
        // tests there is no real DbContext, so the same instance stands in for the reload.
        _connectionRepository
            .Setup(r => r.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        return connection;
    }

    private static ShopeeOrderDetail CreateDetail(string orderSn, string status) => new(
        orderSn, status, "MY", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", null,
        [new ShopeeOrderDetailItem(111, 0, "Item", null, null, 2)]);

    [Fact]
    public async Task RunAsync_OnePageTwoOrders_IngestsBothOrders()
    {
        ShopeeShopConnection connection = CreateConnection(TenantA, ShopIdA, "access-a");
        _connectionRepository
            .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ShopeeShopConnection> { connection });
        _gateway
            .Setup(g => g.GetOrderListAsync(
                ShopIdA, "access-a", It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderList>.Success(new ShopeeOrderList(["SN1", "SN2"], false, null)));
        _orderRepository
            .Setup(r => r.GetByOrderSnAsync(It.IsAny<string>(), TenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopeeOrder?)null);
        _gateway
            .Setup(g => g.GetOrderDetailAsync(ShopIdA, "access-a", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderDetail>.Success(CreateDetail("SN1", "UNPAID")));
        _gateway
            .Setup(g => g.GetOrderDetailAsync(ShopIdA, "access-a", "SN2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderDetail>.Success(CreateDetail("SN2", "UNPAID")));

        ShopeeOrderReconciliationProcessor processor = CreateProcessor();
        await processor.RunAsync(24, 30, CancellationToken.None);

        _gateway.Verify(
            g => g.GetOrderDetailAsync(ShopIdA, "access-a", "SN1", It.IsAny<CancellationToken>()), Times.Once);
        _gateway.Verify(
            g => g.GetOrderDetailAsync(ShopIdA, "access-a", "SN2", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_HasMoreWithCursor_FetchesSecondPageWithReturnedCursor()
    {
        ShopeeShopConnection connection = CreateConnection(TenantA, ShopIdA, "access-a");
        _connectionRepository
            .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ShopeeShopConnection> { connection });
        _gateway
            .Setup(g => g.GetOrderListAsync(
                ShopIdA, "access-a", It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderList>.Success(new ShopeeOrderList(["SN1"], true, "c2")));
        _gateway
            .Setup(g => g.GetOrderListAsync(
                ShopIdA, "access-a", It.IsAny<DateTime>(), It.IsAny<DateTime>(), "c2", It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderList>.Success(new ShopeeOrderList(["SN2"], false, null)));
        _orderRepository
            .Setup(r => r.GetByOrderSnAsync(It.IsAny<string>(), TenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopeeOrder?)null);
        _gateway
            .Setup(g => g.GetOrderDetailAsync(ShopIdA, "access-a", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderDetail>.Success(CreateDetail("SN", "UNPAID")));

        ShopeeOrderReconciliationProcessor processor = CreateProcessor();
        await processor.RunAsync(24, 30, CancellationToken.None);

        _gateway.Verify(
            g => g.GetOrderListAsync(
                ShopIdA, "access-a", It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _gateway.Verify(
            g => g.GetOrderListAsync(
                ShopIdA, "access-a", It.IsAny<DateTime>(), It.IsAny<DateTime>(), "c2", It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private (ShopeeShopConnection Connection, ShopeeOrder Order) CreateStuckOrder()
    {
        ShopeeShopConnection connection = CreateConnection(TenantA, ShopIdA, "access-a");
        Product product = ProductTestFactory.CreateSingle(TenantA, stockQuantity: 10);
        ShopeeOrderSnapshot snapshot = new(
            "READY_TO_SHIP", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", null);
        ShopeeOrder order = ShopeeOrder.Create(
            TenantA, "SN1", "MY", snapshot,
            [new ShopeeOrderItemSnapshot(111, 0, "Item", null, null, 2, product.Id)], Now).Value;
        order.MarkShipmentArranged("u", Now);

        _productRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        _waybillRepository
            .Setup(r => r.ExistsByNumberAsync(It.IsAny<string>(), TenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _connectionRepository
            .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ShopeeShopConnection>());
        _orderRepository
            .Setup(r => r.ListAwaitingTrackingAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ShopeeOrder> { order });
        // Mirrors the per-iteration fresh fetch the tracking retry performs.
        _orderRepository
            .Setup(r => r.GetByIdAsync(order.Id, TenantA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        return (connection, order);
    }

    [Fact]
    public async Task RunAsync_StuckOrderWithTrackingNumber_CompletesShipment()
    {
        (ShopeeShopConnection connection, ShopeeOrder order) = CreateStuckOrder();
        _gateway
            .Setup(g => g.GetTrackingNumberAsync(
                connection.ShopId, connection.AccessToken, order.OrderSn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string?>.Success("TRACK1"));

        ShopeeOrderReconciliationProcessor processor = CreateProcessor();
        await processor.RunAsync(24, 30, CancellationToken.None);

        order.Status.Should().Be(ShopeeOrderStatus.Shipped);
        order.TrackingNumber.Should().Be("TRACK1");
        _waybillRepository.Verify(r => r.Add(It.IsAny<Waybill>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_StuckOrderWithoutTrackingNumber_DoesNotComplete()
    {
        (ShopeeShopConnection connection, ShopeeOrder order) = CreateStuckOrder();
        _gateway
            .Setup(g => g.GetTrackingNumberAsync(
                connection.ShopId, connection.AccessToken, order.OrderSn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string?>.Success(null));

        ShopeeOrderReconciliationProcessor processor = CreateProcessor();
        await processor.RunAsync(24, 30, CancellationToken.None);

        order.Status.Should().Be(ShopeeOrderStatus.AwaitingTracking);
        order.TrackingNumber.Should().BeNull();
        _waybillRepository.Verify(r => r.Add(It.IsAny<Waybill>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_OneConnectionGatewayFails_StillProcessesNextConnection()
    {
        ShopeeShopConnection connectionA = CreateConnection(TenantA, ShopIdA, "access-a");
        ShopeeShopConnection connectionB = CreateConnection(TenantB, ShopIdB, "access-b");
        _connectionRepository
            .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ShopeeShopConnection> { connectionA, connectionB });
        _gateway
            .Setup(g => g.GetOrderListAsync(
                ShopIdA, "access-a", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderList>.Failure(ShopeeOrderErrors.OrderListFetchFailed));
        _gateway
            .Setup(g => g.GetOrderListAsync(
                ShopIdB, "access-b", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderList>.Success(new ShopeeOrderList(["SN-B"], false, null)));
        _orderRepository
            .Setup(r => r.GetByOrderSnAsync("SN-B", TenantB, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopeeOrder?)null);
        _gateway
            .Setup(g => g.GetOrderDetailAsync(ShopIdB, "access-b", "SN-B", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderDetail>.Success(CreateDetail("SN-B", "UNPAID")));

        ShopeeOrderReconciliationProcessor processor = CreateProcessor();
        await processor.RunAsync(24, 30, CancellationToken.None);

        _gateway.Verify(
            g => g.GetOrderDetailAsync(ShopIdB, "access-b", "SN-B", It.IsAny<CancellationToken>()), Times.Once);
    }
}
