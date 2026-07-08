namespace Wrapsfer.Application.Shopee.Services;

public sealed record ShopeeSellableUnit(
    long ItemId,
    long ModelId,
    string ItemName,
    string? ModelName,
    string? Sku,
    int? Stock,
    bool HasModel);
