namespace Wrapsfer.Domain.Enums;

public enum ShopeeOrderStatus
{
    NeedsLinking = 0,
    ReadyToShip = 1,
    AwaitingTracking = 2,
    Shipped = 3,
    Cancelled = 4,
    ShipmentFailed = 5
}
