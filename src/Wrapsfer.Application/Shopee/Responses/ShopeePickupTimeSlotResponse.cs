namespace Wrapsfer.Application.Shopee.Responses;

public sealed record ShopeePickupTimeSlotResponse(string PickupTimeId, DateTime Date, string? TimeText);
