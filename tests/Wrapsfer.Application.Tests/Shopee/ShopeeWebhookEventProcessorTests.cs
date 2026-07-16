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

public sealed class ShopeeWebhookEventProcessorTests
{
    private const string TenantId = "tenant-a";
    private const long ShopId = 1001;
    private static readonly DateTime Now = new(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IShopeeWebhookEventRepository> _eventRepository = new();
    private readonly Mock<IShopeeShopConnectionRepository> _connectionRepository = new();
    private readonly Mock<IShopeeOrderRepository> _orderRepository = new();
    private readonly Mock<IShopeeGateway> _gateway = new();
    private readonly Mock<IShopeeProductLinkRepository> _linkRepository = new();
    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IProductComponentRepository> _productComponentRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public ShopeeWebhookEventProcessorTests() =>
        _linkRepository
            .Setup(r => r.ListByTenantAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ShopeeProductLink>());

    private ShopeeWebhookEventProcessor CreateProcessor()
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

        return new ShopeeWebhookEventProcessor(
            _eventRepository.Object,
            _connectionRepository.Object,
            _orderRepository.Object,
            _gateway.Object,
            ingestionService,
            completionService,
            tokenRefresher,
            _unitOfWork.Object,
            NullLogger<ShopeeWebhookEventProcessor>.Instance);
    }

    private static ShopeeShopConnection CreateConnection() => ShopeeShopConnection.Create(
        TenantId, ShopId, "access-token", "refresh-token",
        Now.AddHours(1), Now.AddDays(30), Now, "linker").Value;

    private static ShopeeWebhookEvent CreateEvent(int code, string payload) =>
        ShopeeWebhookEvent.Create(ShopId, code, Guid.NewGuid().ToString(), payload, Now).Value;

    private static ShopeeOrderDetail CreateDetail(string status) => new(
        "SN1", status, "MY", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", null,
        [new ShopeeOrderDetailItem(111, 0, "Item", null, null, 2)]);

    private void SetPendingEvents(params ShopeeWebhookEvent[] events)
    {
        _eventRepository
            .Setup(r => r.ListPendingAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        // Mirrors the fresh reload the processor performs after a failure/ignored result:
        // in these mock-based tests there is no real DbContext to re-fetch from, so the
        // same in-memory instance stands in for "the row as it exists in the database".
        foreach (ShopeeWebhookEvent webhookEvent in events)
        {
            _eventRepository
                .Setup(r => r.GetByIdAsync(webhookEvent.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(webhookEvent);
        }
    }

    [Fact]
    public async Task RunAsync_OrderStatusEvent_RoutesToIngestionAndMarksProcessed()
    {
        const string payload = """{"code":3,"shop_id":1001,"data":{"ordersn":"SN1"}}""";
        ShopeeWebhookEvent webhookEvent = CreateEvent(ShopeeWebhookEventProcessor.OrderStatusPushCode, payload);
        SetPendingEvents(webhookEvent);
        _connectionRepository
            .Setup(r => r.GetByShopIdAsync(ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConnection());
        _orderRepository
            .Setup(r => r.GetByOrderSnAsync("SN1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopeeOrder?)null);
        _gateway
            .Setup(g => g.GetOrderDetailAsync(ShopId, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderDetail>.Success(CreateDetail("UNPAID")));

        ShopeeWebhookEventProcessor processor = CreateProcessor();
        ShopeeWebhookRunSummary summary = await processor.RunAsync(10, 5, CancellationToken.None);

        summary.ProcessedCount.Should().Be(1);
        summary.FailedCount.Should().Be(0);
        summary.IgnoredCount.Should().Be(0);
        webhookEvent.Status.Should().Be(ShopeeWebhookEventStatus.Processed);
    }

    [Fact]
    public async Task RunAsync_UnknownShopId_MarksEventIgnored()
    {
        const string payload = """{"code":3,"shop_id":1001,"data":{"ordersn":"SN1"}}""";
        ShopeeWebhookEvent webhookEvent = CreateEvent(ShopeeWebhookEventProcessor.OrderStatusPushCode, payload);
        SetPendingEvents(webhookEvent);
        _connectionRepository
            .Setup(r => r.GetByShopIdAsync(ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopeeShopConnection?)null);

        ShopeeWebhookEventProcessor processor = CreateProcessor();
        ShopeeWebhookRunSummary summary = await processor.RunAsync(10, 5, CancellationToken.None);

        summary.IgnoredCount.Should().Be(1);
        summary.FailedCount.Should().Be(0);
        webhookEvent.Status.Should().Be(ShopeeWebhookEventStatus.Ignored);
    }

    [Fact]
    public async Task RunAsync_IngestionFailure_MarksEventFailedWithSingleAttempt()
    {
        const string payload = """{"code":3,"shop_id":1001,"data":{"ordersn":"SN1"}}""";
        ShopeeWebhookEvent webhookEvent = CreateEvent(ShopeeWebhookEventProcessor.OrderStatusPushCode, payload);
        SetPendingEvents(webhookEvent);
        _connectionRepository
            .Setup(r => r.GetByShopIdAsync(ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConnection());
        _orderRepository
            .Setup(r => r.GetByOrderSnAsync("SN1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopeeOrder?)null);
        _gateway
            .Setup(g => g.GetOrderDetailAsync(ShopId, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderDetail>.Failure(ShopeeOrderErrors.OrderDetailFetchFailed));

        ShopeeWebhookEventProcessor processor = CreateProcessor();
        ShopeeWebhookRunSummary summary = await processor.RunAsync(10, 5, CancellationToken.None);

        summary.FailedCount.Should().Be(1);
        summary.ProcessedCount.Should().Be(0);
        webhookEvent.Status.Should().Be(ShopeeWebhookEventStatus.Failed);
        webhookEvent.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_TrackingEventWithPayloadTrackingNumber_CompletesAwaitingTrackingOrder()
    {
        const string payload = """{"code":4,"shop_id":1001,"data":{"ordersn":"SN1","tracking_no":"TRACK1"}}""";
        ShopeeWebhookEvent webhookEvent = CreateEvent(ShopeeWebhookEventProcessor.TrackingNumberPushCode, payload);
        SetPendingEvents(webhookEvent);
        _connectionRepository
            .Setup(r => r.GetByShopIdAsync(ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConnection());

        Product product = ProductTestFactory.CreateSingle(TenantId, stockQuantity: 10);
        ShopeeOrderSnapshot snapshot = new(
            "READY_TO_SHIP", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", null);
        ShopeeOrder order = ShopeeOrder.Create(
            TenantId, "SN1", "MY", snapshot,
            [new ShopeeOrderItemSnapshot(111, 0, "Item", null, null, 2, product.Id)], Now).Value;
        order.MarkShipmentArranged("u", Now);

        _orderRepository
            .Setup(r => r.GetByOrderSnAsync("SN1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _waybillRepository
            .Setup(r => r.ExistsByNumberAsync("TRACK1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _productRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        ShopeeWebhookEventProcessor processor = CreateProcessor();
        ShopeeWebhookRunSummary summary = await processor.RunAsync(10, 5, CancellationToken.None);

        summary.ProcessedCount.Should().Be(1);
        webhookEvent.Status.Should().Be(ShopeeWebhookEventStatus.Processed);
        order.Status.Should().Be(ShopeeOrderStatus.Shipped);
        order.TrackingNumber.Should().Be("TRACK1");
        _waybillRepository.Verify(r => r.Add(It.IsAny<Waybill>()), Times.Once);
        _gateway.Verify(
            g => g.GetTrackingNumberAsync(
                It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_CancelledPushWithFailedStockRelease_ClearsTrackerAndEndsEventFailed()
    {
        // A CANCELLED push for a shipped order: HandleCancellationAsync marks the order
        // Cancelled and cancels its waybill, then stock release fails because reserved
        // quantity (1) is short of the waybill item quantity (2). The half-applied
        // cancellation must never reach the event's own failure save.
        Product product = ProductTestFactory.CreateSingle(TenantId, stockQuantity: 10);
        product.Reserve(1);
        Waybill waybill = Waybill.Create(TenantId, new DateOnly(2026, 7, 16), "TRACK1").Value;
        waybill.AddOrIncrementItem(product.Id, product.Barcode, 2);

        ShopeeOrderSnapshot initialSnapshot = new(
            "READY_TO_SHIP", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", null);
        ShopeeOrder order = ShopeeOrder.Create(
            TenantId, "SN1", "MY", initialSnapshot,
            [new ShopeeOrderItemSnapshot(111, 0, "Item", null, null, 2, product.Id)], Now).Value;
        order.MarkShipmentArranged("u", Now);
        order.AssignTracking("TRACK1");
        order.LinkWaybill(waybill.Id);

        const string payload = """{"code":3,"shop_id":1001,"data":{"ordersn":"SN1"}}""";
        ShopeeWebhookEvent webhookEvent = CreateEvent(ShopeeWebhookEventProcessor.OrderStatusPushCode, payload);
        SetPendingEvents(webhookEvent);
        _connectionRepository
            .Setup(r => r.GetByShopIdAsync(ShopId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConnection());
        _orderRepository
            .Setup(r => r.GetByOrderSnAsync("SN1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _gateway
            .Setup(g => g.GetOrderDetailAsync(ShopId, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderDetail>.Success(CreateDetail("CANCELLED")));
        _waybillRepository
            .Setup(r => r.GetByIdWithItemsAsync(waybill.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybill);
        _productRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        ShopeeWebhookEventProcessor processor = CreateProcessor();
        ShopeeWebhookRunSummary summary = await processor.RunAsync(10, 5, CancellationToken.None);

        summary.FailedCount.Should().Be(1);
        summary.ProcessedCount.Should().Be(0);
        webhookEvent.Status.Should().Be(ShopeeWebhookEventStatus.Failed);
        order.Status.Should().Be(ShopeeOrderStatus.Cancelled);
        _unitOfWork.Verify(u => u.ClearChangeTracker(), Times.AtLeastOnce);
        _eventRepository.Verify(
            r => r.GetByIdAsync(webhookEvent.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
