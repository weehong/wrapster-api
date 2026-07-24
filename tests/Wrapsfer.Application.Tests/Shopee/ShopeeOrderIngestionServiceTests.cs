using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Application.Tests.Helpers;
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
    private readonly Mock<IFulfillmentDelegationRepository> _delegationRepository = new();
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
            _delegationRepository.Object,
            _productRepository.Object,
            cancellationService,
            _unitOfWork.Object,
            NullLogger<ShopeeOrderIngestionService>.Instance);

        _delegationRepository
            .Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FulfillmentDelegation?)null);

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

    private static FulfillmentDelegation CreateActiveDelegation()
    {
        FulfillmentDelegation delegation =
            FulfillmentDelegation.Request(TenantId, FulfillmentShippingMethod.Dropoff).Value;
        delegation.Accept();
        return delegation;
    }

    private static ShopeeOrderDetail CreateDetailWithSku(string? sku) => new(
        "SN1", "READY_TO_SHIP", "MY", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", null,
        [new ShopeeOrderDetailItem(111, 0, "Item", null, sku, 2)]);

    private void SetupUnknownOrder(string? sku)
    {
        _orderRepository
            .Setup(r => r.GetByOrderSnAsync("SN1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShopeeOrder?)null);
        _gateway
            .Setup(g => g.GetOrderDetailAsync(1001, "access-token", "SN1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ShopeeOrderDetail>.Success(CreateDetailWithSku(sku)));
    }

    [Fact]
    public async Task IngestOrderAsync_NoLink_ActiveDelegation_SkuMatchesBarcode_ResolvesAndPersistsLink()
    {
        Product product = ProductTestFactory.CreateSingle(TenantId, barcode: "BC-1");
        SetupUnknownOrder("BC-1");
        _delegationRepository
            .Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateActiveDelegation());
        _productRepository
            .Setup(r => r.GetByBarcodesAsync(
                It.Is<IEnumerable<string>>(b => b.Single() == "BC-1"),
                TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });
        ShopeeOrder? added = null;
        _orderRepository.Setup(r => r.Add(It.IsAny<ShopeeOrder>()))
            .Callback<ShopeeOrder>(o => added = o);

        Result result = await _service.IngestOrderAsync(CreateConnection(), "SN1", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        added.Should().NotBeNull();
        added!.Status.Should().Be(ShopeeOrderStatus.ReadyToShip);
        added.Items.Single().ProductId.Should().Be(product.Id);
        _linkRepository.Verify(r => r.Add(It.Is<ShopeeProductLink>(l =>
            l.TenantId == TenantId
            && l.ProductId == product.Id
            && l.ShopeeItemId == 111
            && l.ShopeeModelId == 0
            && l.LinkedBy == "system:auto-match")), Times.Once);
    }

    [Fact]
    public async Task IngestOrderAsync_NoLink_NoDelegation_DoesNotAutoMatch()
    {
        SetupUnknownOrder("BC-1");
        ShopeeOrder? added = null;
        _orderRepository.Setup(r => r.Add(It.IsAny<ShopeeOrder>()))
            .Callback<ShopeeOrder>(o => added = o);

        Result result = await _service.IngestOrderAsync(CreateConnection(), "SN1", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        added!.Status.Should().Be(ShopeeOrderStatus.NeedsLinking);
        _productRepository.Verify(r => r.GetByBarcodesAsync(
            It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _linkRepository.Verify(r => r.Add(It.IsAny<ShopeeProductLink>()), Times.Never);
    }

    [Fact]
    public async Task IngestOrderAsync_NoLink_RequestedDelegation_DoesNotAutoMatch()
    {
        SetupUnknownOrder("BC-1");
        _delegationRepository
            .Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FulfillmentDelegation.Request(TenantId, FulfillmentShippingMethod.Dropoff).Value);
        ShopeeOrder? added = null;
        _orderRepository.Setup(r => r.Add(It.IsAny<ShopeeOrder>()))
            .Callback<ShopeeOrder>(o => added = o);

        Result result = await _service.IngestOrderAsync(CreateConnection(), "SN1", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        added!.Status.Should().Be(ShopeeOrderStatus.NeedsLinking);
        _productRepository.Verify(r => r.GetByBarcodesAsync(
            It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestOrderAsync_NoLink_ActiveDelegation_BlankSku_DoesNotLookupProducts()
    {
        SetupUnknownOrder("  ");
        _delegationRepository
            .Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateActiveDelegation());
        ShopeeOrder? added = null;
        _orderRepository.Setup(r => r.Add(It.IsAny<ShopeeOrder>()))
            .Callback<ShopeeOrder>(o => added = o);

        Result result = await _service.IngestOrderAsync(CreateConnection(), "SN1", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        added!.Status.Should().Be(ShopeeOrderStatus.NeedsLinking);
        _productRepository.Verify(r => r.GetByBarcodesAsync(
            It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestOrderAsync_NoLink_ActiveDelegation_BarcodeMiss_StaysNeedsLinking()
    {
        SetupUnknownOrder("BC-UNKNOWN");
        _delegationRepository
            .Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateActiveDelegation());
        _productRepository
            .Setup(r => r.GetByBarcodesAsync(
                It.IsAny<IEnumerable<string>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product>());
        ShopeeOrder? added = null;
        _orderRepository.Setup(r => r.Add(It.IsAny<ShopeeOrder>()))
            .Callback<ShopeeOrder>(o => added = o);

        Result result = await _service.IngestOrderAsync(CreateConnection(), "SN1", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        added!.Status.Should().Be(ShopeeOrderStatus.NeedsLinking);
        _linkRepository.Verify(r => r.Add(It.IsAny<ShopeeProductLink>()), Times.Never);
    }
}
