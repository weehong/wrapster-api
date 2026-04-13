using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Domain.Enums;

namespace Wrapster.Application.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Barcode,
    string Name,
    string? SkuCode,
    ProductType Type,
    decimal Cost,
    int StockQuantity,
    int? LowStockThreshold,
    Guid? UnpackTargetProductId = null,
    int? UnpackQuantityPerPackage = null,
    IReadOnlyList<BundleComponentInput>? Components = null) : ICommand<Guid>;
