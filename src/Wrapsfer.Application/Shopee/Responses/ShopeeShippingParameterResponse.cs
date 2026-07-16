namespace Wrapsfer.Application.Shopee.Responses;

public sealed record ShopeeShippingParameterResponse(
    bool SupportsPickup,
    bool SupportsDropoff,
    IReadOnlyList<ShopeePickupAddressResponse> PickupAddresses,
    IReadOnlyList<ShopeeDropoffBranchResponse> DropoffBranches);
