namespace Wrapster.Api.Contracts;

public sealed record UpdateProductRequest(
    string? Name,
    string? SkuCode,
    bool ClearSkuCode = false,
    decimal? Cost = null,
    int? LowStockThreshold = null,
    bool ClearLowStockThreshold = false,
    Guid? UnpackTargetProductId = null,
    int? UnpackQuantityPerPackage = null);
