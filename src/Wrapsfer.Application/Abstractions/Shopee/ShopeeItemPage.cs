namespace Wrapsfer.Application.Abstractions.Shopee;

public sealed record ShopeeItemPage(
    IReadOnlyList<long> ItemIds,
    bool HasNextPage,
    int NextOffset,
    int TotalCount);
