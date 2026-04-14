using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Products.Commands.CreateProduct;

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
