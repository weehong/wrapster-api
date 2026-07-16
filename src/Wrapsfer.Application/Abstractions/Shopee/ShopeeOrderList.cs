namespace Wrapsfer.Application.Abstractions.Shopee;

public sealed record ShopeeOrderList(IReadOnlyList<string> OrderSns, bool HasMore, string? NextCursor);
