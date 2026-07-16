namespace Wrapsfer.Application.Abstractions.Shopee;

public sealed record ShopeePickupAddress(
    long AddressId, string Address, IReadOnlyList<ShopeePickupTimeSlot> TimeSlots);
