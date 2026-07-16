namespace Wrapsfer.Api.Contracts;

public sealed record ShipShopeeOrderRequest(
    string Method,
    long? AddressId,
    string? PickupTimeId,
    long? BranchId);
