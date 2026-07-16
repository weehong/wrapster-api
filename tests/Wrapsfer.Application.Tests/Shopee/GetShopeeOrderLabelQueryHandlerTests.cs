using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Abstractions.Storage;
using Wrapsfer.Application.Shopee.Queries.GetShopeeOrderLabel;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee;

public sealed class GetShopeeOrderLabelQueryHandlerTests
{
    private const string TenantId = "tenant-a";
    private const string OrderSn = "SN1";
    private static readonly DateTime Now = new(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OrderId = Guid.NewGuid();
    private readonly Mock<IShopeeOrderRepository> _orderRepository = new();
    private readonly Mock<IShopeeShopConnectionRepository> _connectionRepository = new();
    private readonly Mock<IShopeeGateway> _gateway = new();
    private readonly Mock<IReportStorage> _reportStorage = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ShopeeConnectionTokenRefresher _tokenRefresher;
    private readonly GetShopeeOrderLabelQueryHandler _handler;

    public GetShopeeOrderLabelQueryHandlerTests()
    {
        _tokenRefresher = new ShopeeConnectionTokenRefresher(
            _gateway.Object, _unitOfWork.Object, NullLogger<ShopeeConnectionTokenRefresher>.Instance);
        _handler = new GetShopeeOrderLabelQueryHandler(
            _orderRepository.Object,
            _connectionRepository.Object,
            _gateway.Object,
            _tokenRefresher,
            _reportStorage.Object,
            _unitOfWork.Object);
    }

    [Fact]
    public async Task Handle_OrderNotShipped_ReturnsNotShipped()
    {
        ShopeeOrder order = CreateOrder(shipped: false);
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        Result<ShopeeOrderLabelResult> result = await _handler.Handle(
            new GetShopeeOrderLabelQuery(TenantId, OrderId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.NotShipped);
        _gateway.Verify(g => g.DownloadShippingDocumentAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_StoredLabelExists_ServesFromStorageWithoutTouchingGateway()
    {
        ShopeeOrder order = CreateOrder(shipped: true);
        order.MarkLabelStored("shopee-labels/tenant-a/SN1.pdf");
        byte[] storedBytes = [1, 2, 3];
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _reportStorage
            .Setup(s => s.DownloadAsync("shopee-labels/tenant-a/SN1.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedBytes);

        Result<ShopeeOrderLabelResult> result = await _handler.Handle(
            new GetShopeeOrderLabelQuery(TenantId, OrderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().BeEquivalentTo(storedBytes);
        result.Value.ContentType.Should().Be("application/pdf");
        result.Value.FileName.Should().Be($"shopee-awb-{OrderSn}.pdf");
        _gateway.Verify(g => g.DownloadShippingDocumentAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _connectionRepository.Verify(r => r.GetByTenantIdAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoStoredLabel_FetchesFromGatewayUploadsAndPersists()
    {
        ShopeeOrder order = CreateOrder(shipped: true);
        ShopeeShopConnection connection = CreateConnection();
        byte[] gatewayBytes = [9, 9, 9];
        StoredReport storedReport = new(
            "bucket", "shopee-labels/tenant-a/SN1.pdf", "https://example.com/download", Now.AddHours(1));
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _connectionRepository
            .Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);
        _gateway
            .Setup(g => g.DownloadShippingDocumentAsync(
                connection.ShopId, connection.AccessToken, OrderSn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(gatewayBytes));
        _reportStorage
            .Setup(s => s.UploadAsync(
                $"shopee-labels/{TenantId}/{OrderSn}.pdf", gatewayBytes, "application/pdf",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedReport);

        Result<ShopeeOrderLabelResult> result = await _handler.Handle(
            new GetShopeeOrderLabelQuery(TenantId, OrderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().BeEquivalentTo(gatewayBytes);
        result.Value.ContentType.Should().Be("application/pdf");
        result.Value.FileName.Should().Be($"shopee-awb-{OrderSn}.pdf");
        order.LabelStorageKey.Should().Be(storedReport.ObjectKey);
        order.LabelPrintedAt.Should().NotBeNull();
        _reportStorage.Verify(s => s.UploadAsync(
            $"shopee-labels/{TenantId}/{OrderSn}.pdf", gatewayBytes, "application/pdf",
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StoredKeyButObjectMissing_FallsThroughToGatewayRefetch()
    {
        ShopeeOrder order = CreateOrder(shipped: true);
        order.MarkLabelStored("shopee-labels/tenant-a/SN1.pdf");
        ShopeeShopConnection connection = CreateConnection();
        byte[] gatewayBytes = [4, 5, 6];
        StoredReport storedReport = new(
            "bucket", "shopee-labels/tenant-a/SN1.pdf", "https://example.com/download", Now.AddHours(1));
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _reportStorage
            .Setup(s => s.DownloadAsync("shopee-labels/tenant-a/SN1.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        _connectionRepository
            .Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);
        _gateway
            .Setup(g => g.DownloadShippingDocumentAsync(
                connection.ShopId, connection.AccessToken, OrderSn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(gatewayBytes));
        _reportStorage
            .Setup(s => s.UploadAsync(
                $"shopee-labels/{TenantId}/{OrderSn}.pdf", gatewayBytes, "application/pdf",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedReport);

        Result<ShopeeOrderLabelResult> result = await _handler.Handle(
            new GetShopeeOrderLabelQuery(TenantId, OrderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().BeEquivalentTo(gatewayBytes);
        _gateway.Verify(g => g.DownloadShippingDocumentAsync(
            connection.ShopId, connection.AccessToken, OrderSn, It.IsAny<CancellationToken>()), Times.Once);
        _reportStorage.Verify(s => s.UploadAsync(
            $"shopee-labels/{TenantId}/{OrderSn}.pdf", gatewayBytes, "application/pdf",
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ShopeeOrder CreateOrder(bool shipped)
    {
        ShopeeOrderSnapshot snapshot = new(
            "READY_TO_SHIP", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", null);
        Guid productId = Guid.NewGuid();
        ShopeeOrder order = ShopeeOrder.Create(
            TenantId, OrderSn, "MY", snapshot,
            [new ShopeeOrderItemSnapshot(1, 0, "n", null, null, 2, productId)], Now).Value;
        if (!shipped)
        {
            return order;
        }

        order.MarkShipmentArranged("user", Now);
        order.AssignTracking("TRACK1");
        order.LinkWaybill(Guid.NewGuid());
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
