namespace Wrapsfer.Application.Shopee.Responses;

public sealed record ShopeePickupAddressResponse(
    long AddressId,
    string Address,
    IReadOnlyList<ShopeePickupTimeSlotResponse> TimeSlots);
