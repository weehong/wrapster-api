using FluentAssertions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Tests.Entities;

public sealed class ShopeeOrderTests
{
    private static readonly DateTime Now = new(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);

    private static ShopeeOrderSnapshot Snapshot(string status = "READY_TO_SHIP") =>
        new(status, "buyer1", "Jane", "+60123456789", "1 Jalan Test, KL",
            59.90m, "MYR", null, "SPX Express", Now.AddDays(2));

    private static ShopeeOrderItemSnapshot Item(Guid? productId, long itemId = 111, int quantity = 2) =>
        new(itemId, 0, "Item name", null, "SKU-1", quantity, productId);

    private static ShopeeOrder CreateOrder(params ShopeeOrderItemSnapshot[] items)
    {
        Result<ShopeeOrder> result = ShopeeOrder.Create(
            "tenant-a", "260716ABC123", "MY", Snapshot(), items, Now);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public void Create_WithAllItemsLinked_StartsReadyToShip()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        order.Status.Should().Be(ShopeeOrderStatus.ReadyToShip);
        order.HasUnresolvedItems.Should().BeFalse();
        order.Items.Should().HaveCount(1);
    }

    [Fact]
    public void Create_WithUnlinkedItem_StartsNeedsLinking()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()), Item(null, itemId: 222));
        order.Status.Should().Be(ShopeeOrderStatus.NeedsLinking);
        order.HasUnresolvedItems.Should().BeTrue();
    }

    [Fact]
    public void Create_WithoutItems_Fails()
    {
        Result<ShopeeOrder> result = ShopeeOrder.Create(
            "tenant-a", "260716ABC123", "MY", Snapshot(), [], Now);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.NoItems);
    }

    [Fact]
    public void ApplyShopeeSnapshot_ResolvingAllItems_MovesToReadyToShip()
    {
        ShopeeOrder order = CreateOrder(Item(null));
        Result result = order.ApplyShopeeSnapshot(Snapshot(), [Item(Guid.NewGuid())], Now.AddMinutes(5));
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.ReadyToShip);
        order.LastSyncedAt.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public void ApplyShopeeSnapshot_DoesNotDowngradeShippedOrder()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        order.MarkShipmentArranged("user1", Now);
        order.AssignTracking("MYTRACK123");
        order.LinkWaybill(Guid.NewGuid());
        order.ApplyShopeeSnapshot(Snapshot("SHIPPED"), [Item(null)], Now.AddHours(1));
        order.Status.Should().Be(ShopeeOrderStatus.Shipped);
    }

    [Fact]
    public void MarkShipmentArranged_FromReadyToShip_MovesToAwaitingTracking()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        Result result = order.MarkShipmentArranged("user1", Now);
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.AwaitingTracking);
        order.ShipmentArrangedBy.Should().Be("user1");
        order.ShipmentArrangedAt.Should().Be(Now);
        order.LastShipError.Should().BeNull();
    }

    [Fact]
    public void MarkShipmentArranged_FromNeedsLinking_Fails()
    {
        ShopeeOrder order = CreateOrder(Item(null));
        Result result = order.MarkShipmentArranged("user1", Now);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.NotReadyToShip);
    }

    [Fact]
    public void MarkShipmentFailed_ThenArrange_IsRetryable()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        order.MarkShipmentArranged("user1", Now);
        Result failResult = order.MarkShipmentFailed("courier rejected", Now);
        failResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.ShipmentFailed);
        order.LastShipError.Should().Be("courier rejected");
        Result retryResult = order.MarkShipmentArranged("user1", Now.AddMinutes(1));
        retryResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.AwaitingTracking);
    }

    [Fact]
    public void AssignTracking_ThenLinkWaybill_MovesToShipped()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        order.MarkShipmentArranged("user1", Now);
        Result trackResult = order.AssignTracking("MYTRACK123");
        trackResult.IsSuccess.Should().BeTrue();
        Guid waybillId = Guid.NewGuid();
        Result linkResult = order.LinkWaybill(waybillId);
        linkResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.Shipped);
        order.TrackingNumber.Should().Be("MYTRACK123");
        order.WaybillId.Should().Be(waybillId);
    }

    [Fact]
    public void LinkWaybill_WithoutTracking_Fails()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        order.MarkShipmentArranged("user1", Now);
        Result result = order.LinkWaybill(Guid.NewGuid());
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.TrackingNotAssigned);
    }

    [Fact]
    public void AssignTracking_WhenNotAwaitingTracking_Fails()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        Result result = order.AssignTracking("MYTRACK123");
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.NotAwaitingTracking);
    }

    [Fact]
    public void MarkCancelled_FromAnyActiveStatus_Succeeds_AndIsTerminal()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        Result first = order.MarkCancelled(Now);
        first.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(ShopeeOrderStatus.Cancelled);
        order.CancelledAt.Should().Be(Now);
        Result second = order.MarkCancelled(Now.AddMinutes(1));
        second.IsFailure.Should().BeTrue();
        second.Error.Should().Be(ShopeeOrderErrors.AlreadyCancelled);
    }

    [Fact]
    public void MarkLabelPrinted_IsSetOnce()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        order.MarkShipmentArranged("user1", Now);
        order.AssignTracking("MYTRACK123");
        order.LinkWaybill(Guid.NewGuid());
        order.MarkLabelStored("shopee-labels/tenant-a/260716ABC123.pdf").IsSuccess.Should().BeTrue();
        order.MarkLabelPrinted(Now).IsSuccess.Should().BeTrue();
        order.MarkLabelPrinted(Now.AddHours(1)).IsSuccess.Should().BeTrue();
        order.LabelPrintedAt.Should().Be(Now);
    }

    [Fact]
    public void MarkShipmentFailed_WhenCancelled_FailsWithCannotFailShipment()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        order.MarkCancelled(Now);
        Result result = order.MarkShipmentFailed("x", Now);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.CannotFailShipment);
    }

    [Fact]
    public void MarkLabelStored_WhenNotShipped_Fails()
    {
        ShopeeOrder order = CreateOrder(Item(Guid.NewGuid()));
        Result result = order.MarkLabelStored("key");
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.NotShipped);
    }
}
