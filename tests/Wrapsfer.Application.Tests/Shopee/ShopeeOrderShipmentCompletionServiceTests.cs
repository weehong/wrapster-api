using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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

public sealed class ShopeeOrderShipmentCompletionServiceTests
{
    private const string TenantId = "tenant-a";
    private const string TrackingNumber = "TRACK1";
    private static readonly DateTime Now = new(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);
    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IProductComponentRepository> _productComponentRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private static ShopeeOrder CreateOrder(Guid? productId, bool arranged)
    {
        ShopeeOrderSnapshot snapshot = new(
            "READY_TO_SHIP", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", null);
        ShopeeOrder order = ShopeeOrder.Create(
            TenantId, "SN1", "MY", snapshot,
            [new ShopeeOrderItemSnapshot(1, 0, "n", null, null, 2, productId)], Now).Value;
        if (arranged)
        {
            order.MarkShipmentArranged("u", Now);
        }
        return order;
    }

    private ShopeeOrderShipmentCompletionService CreateService() =>
        new(
            _waybillRepository.Object,
            _productRepository.Object,
            new StockReservationService(_productRepository.Object, _productComponentRepository.Object),
            _unitOfWork.Object,
            NullLogger<ShopeeOrderShipmentCompletionService>.Instance);

    [Fact]
    public async Task CompleteAsync_HappyPath_CreatesWaybillAndShipsOrder()
    {
        Product product = ProductTestFactory.CreateSingle(TenantId, stockQuantity: 10);
        ShopeeOrder order = CreateOrder(product.Id, arranged: true);
        _waybillRepository
            .Setup(r => r.ExistsByNumberAsync(TrackingNumber, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _productRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        ShopeeOrderShipmentCompletionService service = CreateService();
        Result result = await service.CompleteAsync(order, TrackingNumber, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _waybillRepository.Verify(r => r.Add(It.IsAny<Waybill>()), Times.Once);
        order.Status.Should().Be(ShopeeOrderStatus.Shipped);
        order.TrackingNumber.Should().Be(TrackingNumber);
        order.WaybillId.Should().NotBeNull();
        product.ReservedQuantity.Should().Be(2);
    }

    [Fact]
    public async Task CompleteAsync_TrackingNumberAlreadyUsed_FailsShipmentWithConflict()
    {
        Product product = ProductTestFactory.CreateSingle(TenantId, stockQuantity: 10);
        ShopeeOrder order = CreateOrder(product.Id, arranged: true);
        _waybillRepository
            .Setup(r => r.ExistsByNumberAsync(TrackingNumber, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        ShopeeOrderShipmentCompletionService service = CreateService();
        Result result = await service.CompleteAsync(order, TrackingNumber, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.TrackingNumberConflict);
        order.Status.Should().Be(ShopeeOrderStatus.ShipmentFailed);
        order.LastShipError.Should().Be(ShopeeOrderErrors.TrackingNumberConflict.Description);
        _waybillRepository.Verify(r => r.Add(It.IsAny<Waybill>()), Times.Never);
    }

    [Fact]
    public async Task CompleteAsync_ReservationFails_FailsShipmentAndReturnsReservationError()
    {
        Product product = ProductTestFactory.CreateSingle(TenantId, stockQuantity: 1);
        ShopeeOrder order = CreateOrder(product.Id, arranged: true);
        _waybillRepository
            .Setup(r => r.ExistsByNumberAsync(TrackingNumber, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _productRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        ShopeeOrderShipmentCompletionService service = CreateService();
        Result result = await service.CompleteAsync(order, TrackingNumber, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.InsufficientStock);
        order.Status.Should().Be(ShopeeOrderStatus.ShipmentFailed);
        _waybillRepository.Verify(r => r.Add(It.IsAny<Waybill>()), Times.Never);
    }

    [Fact]
    public async Task CompleteAsync_OrderNotAwaitingTracking_FailsWithoutMutating()
    {
        ShopeeOrder order = CreateOrder(Guid.NewGuid(), arranged: false);

        ShopeeOrderShipmentCompletionService service = CreateService();
        Result result = await service.CompleteAsync(order, TrackingNumber, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.NotAwaitingTracking);
        order.Status.Should().Be(ShopeeOrderStatus.ReadyToShip);
        order.TrackingNumber.Should().BeNull();
        order.WaybillId.Should().BeNull();
        order.LastShipError.Should().BeNull();
        _waybillRepository.Verify(r => r.Add(It.IsAny<Waybill>()), Times.Never);
    }
}
