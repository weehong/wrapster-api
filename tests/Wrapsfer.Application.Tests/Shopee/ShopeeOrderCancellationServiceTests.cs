using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Application.Tests.Helpers;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Shopee;

public sealed class ShopeeOrderCancellationServiceTests
{
    private static readonly DateTime Now = new(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);
    private readonly Mock<IWaybillRepository> _waybillRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IProductComponentRepository> _productComponentRepository = new();

    private static ShopeeOrder CreateOrder(Guid? waybillId = null)
    {
        ShopeeOrderSnapshot snapshot = new(
            "READY_TO_SHIP", "buyer", "Jane", null, null, 10m, "MYR", null, "SPX", null);
        ShopeeOrder order = ShopeeOrder.Create(
            "tenant-a", "SN1", "MY", snapshot,
            [new ShopeeOrderItemSnapshot(1, 0, "n", null, null, 1, Guid.NewGuid())], Now).Value;
        if (waybillId.HasValue)
        {
            order.MarkShipmentArranged("u", Now);
            order.AssignTracking("TRACK1");
            order.LinkWaybill(waybillId.Value);
        }
        return order;
    }

    private ShopeeOrderCancellationService CreateService() =>
        new(
            _waybillRepository.Object,
            new StockReservationService(_productRepository.Object, _productComponentRepository.Object),
            NullLogger<ShopeeOrderCancellationService>.Instance);

    [Fact]
    public async Task HandleCancellation_WithoutWaybill_JustCancelsOrder()
    {
        ShopeeOrderCancellationService service = CreateService();
        ShopeeOrder order = CreateOrder();
        Result result = await service.HandleCancellationAsync(order, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.Cancelled);
    }

    [Fact]
    public async Task HandleCancellation_WithDraftWaybill_CancelsWaybillAndReleasesStock()
    {
        Product product = ProductTestFactory.CreateSingle("tenant-a", stockQuantity: 10);
        product.Reserve(2);
        Waybill waybill = Waybill.Create("tenant-a", new DateOnly(2026, 7, 16), "TRACK1").Value;
        waybill.AddOrIncrementItem(product.Id, product.Barcode, 2);
        _waybillRepository
            .Setup(r => r.GetByIdWithItemsAsync(waybill.Id, "tenant-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybill);
        _productRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), "tenant-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Product> { product });

        ShopeeOrderCancellationService service = CreateService();
        ShopeeOrder order = CreateOrder(waybill.Id);
        Result result = await service.HandleCancellationAsync(order, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        waybill.Status.Should().Be(WaybillStatus.Cancelled);
        waybill.CancellationReason.Should().Be(ShopeeOrderCancellationService.CancellationReason);
        product.ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task HandleCancellation_WithHandedOffWaybill_LeavesWaybillUntouched()
    {
        Waybill waybill = Waybill.Create("tenant-a", new DateOnly(2026, 7, 16), "TRACK1").Value;
        waybill.AddOrIncrementItem(Guid.NewGuid(), "BARCODE1", 2);
        waybill.MarkPacked();
        waybill.MarkHandedOff(waybill.CreatedBy ?? "creator", actingUserIsAdmin: true);
        _waybillRepository
            .Setup(r => r.GetByIdWithItemsAsync(waybill.Id, "tenant-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybill);

        ShopeeOrderCancellationService service = CreateService();
        ShopeeOrder order = CreateOrder(waybill.Id);
        Result result = await service.HandleCancellationAsync(order, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.Cancelled);
        waybill.Status.Should().Be(WaybillStatus.HandedOff);
    }
}
