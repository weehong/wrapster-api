namespace Wrapsfer.Application.Abstractions.Shopee;

public sealed record ShopeeShippingParameter(bool SupportsPickup, bool SupportsDropoff,
    IReadOnlyList<ShopeePickupAddress> PickupAddresses, IReadOnlyList<ShopeeDropoffBranch> DropoffBranches);
