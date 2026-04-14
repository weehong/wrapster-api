using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Products.Commands.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid Id,
    string? Name,
    string? SkuCode,
    bool ClearSkuCode,
    decimal? Cost,
    int? LowStockThreshold,
    bool ClearLowStockThreshold,
    Guid? UnpackTargetProductId = null,
    int? UnpackQuantityPerPackage = null) : ICommand;
