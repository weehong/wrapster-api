using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Shopee.Responses;

namespace Wrapsfer.Application.Shopee.Commands.CreateShopeeLinkedProduct;

public sealed record CreateShopeeLinkedProductCommand(
    string TenantId,
    long ShopeeItemId,
    long ShopeeModelId,
    string Barcode,
    string Name,
    string? SkuCode,
    decimal Cost,
    int StockQuantity,
    int? LowStockThreshold) : ICommand<ShopeeProductLinkResponse>;
