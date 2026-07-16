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

public sealed class ShopeeOrderIngestionServiceTests
{
    private const string TenantId = "tenant-a";
    private static readonly DateTime Now = new(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IShopeeGateway> _gateway = new();
    private readonly Mock<IShopeeOrderRepository> _orderRepository = new();
    private readonly Mock<IShopeeProductLinkRepository> _linkRepository = new();
    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IProductComponentRepository> _productComponentRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ShopeeOrderIngestionService _service;

    public ShopeeOrderIngestionServiceTests()
    {
        ShopeeOrderCancellationService cancellationService = new(
            _waybillRepository.Object,
            new StockReservationService(_productRepository.Object, _productComponentRepository.Object),
            NullLogger<ShopeeOrderCancellationService>.Instance);

        _service = new ShopeeOrderIngestionService(
            _gateway.Object,
            _orderRepository.Object,
            _linkRepository.Object,
            cancellationService,
            _unitOfWork.Object,
            NullLogger<ShopeeOrderIngestionService>.Instance);

        _linkRepository
            .Setup(r => r.ListByTenantAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ShopeeProductLink>());
    }

    private static ShopeeShopConnection CreateConnection() => ShopeeShopConnection.Create(
        TenantId, 1001, "access-token", "refresh-token",
        Now.AddHours(1), Now.AddDays(30), Now, "linker").Value;

    private static ShopeeOrderDetail CreateDetail(string status) => new(
        "SN1", status, "MY", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", null,
        [new ShopeeOrderDetailItem(111, 0, "Item", null, null, 2)]);

    [Fact]
    public async Task IngestOrderAsync_UnknownOrderReadyToShip_LinkPresent_CreatesReadyToShipOrder()
    {
        Guid productId = Guid.NewGuid();
        _linkRepository
            .Setup(r => r.ListByTenantAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ShopeeProductLink> { ShopeeProductLink.Create(
                TenantId, productId, 111, 0, "Item", null, null, "linker").Value });
        _orderRepository
            .Setup(r => r.GetByOrderSnAsync("SN1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopeeOrder?)null);
        _gateway
            .Setup(g => g.GetOrderDetailAsync(1001, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderDetail>.Success(CreateDetail("READY_TO_SHIP")));

        ShopeeShopConnection connection = CreateConnection();
        Result result = await _service.IngestOrderAsync(connection, "SN1", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _orderRepository.Verify(r => r.Add(It.IsAny<ShopeeOrder>()), Times.Once());
        ShopeeOrder createdOrder = (ShopeeOrder)_orderRepository.Invocations
            .Single(i => i.Method.Name == nameof(IShopeeOrderRepository.Add)).Arguments[0];
        createdOrder.Status.Should().Be(ShopeeOrderStatus.ReadyToShip);
        createdOrder.Items.Should().ContainSingle(i => i.ProductId == productId);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestOrderAsync_UnknownOrderReadyToShip_LinkAbsent_CreatesNeedsLinkingOrder()
    {
        _orderRepository
            .Setup(r => r.GetByOrderSnAsync("SN1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopeeOrder?)null);
        _gateway
            .Setup(g => g.GetOrderDetailAsync(1001, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderDetail>.Success(CreateDetail("READY_TO_SHIP")));

        ShopeeShopConnection connection = CreateConnection();
        Result result = await _service.IngestOrderAsync(connection, "SN1", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ShopeeOrder createdOrder = (ShopeeOrder)_orderRepository.Invocations
            .First(i => i.Method.Name == nameof(IShopeeOrderRepository.Add)).Arguments[0];
        createdOrder.Status.Should().Be(ShopeeOrderStatus.NeedsLinking);
        createdOrder.Items.Should().ContainSingle(i => i.ProductId == null);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestOrderAsync_UnknownOrderUnpaid_StoresNothing()
    {
        _orderRepository
            .Setup(r => r.GetByOrderSnAsync("SN1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopeeOrder?)null);
        _gateway
            .Setup(g => g.GetOrderDetailAsync(1001, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderDetail>.Success(CreateDetail("UNPAID")));

        ShopeeShopConnection connection = CreateConnection();
        Result result = await _service.IngestOrderAsync(connection, "SN1", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _orderRepository.Verify(r => r.Add(It.IsAny<ShopeeOrder>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestOrderAsync_KnownOrderCancelledDetail_InvokesCancellation()
    {
        ShopeeOrderSnapshot snapshot = new(
            "READY_TO_SHIP", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", null);
        ShopeeOrder order = ShopeeOrder.Create(
            TenantId, "SN1", "MY", snapshot,
            [new ShopeeOrderItemSnapshot(111, 0, "Item", null, null, 2, Guid.NewGuid())], Now).Value;

        _orderRepository
            .Setup(r => r.GetByOrderSnAsync("SN1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _gateway
            .Setup(g => g.GetOrderDetailAsync(1001, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderDetail>.Success(CreateDetail("CANCELLED")));

        ShopeeShopConnection connection = CreateConnection();
        Result result = await _service.IngestOrderAsync(connection, "SN1", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.Cancelled);
        order.ShopeeStatus.Should().Be("CANCELLED");
        _orderRepository.Verify(r => r.Add(It.IsAny<ShopeeOrder>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
