namespace Wrapsfer.Application.Products.Services;

public sealed record LowStockAlertContext(
    string TenantId,
    Guid ProductId,
    string ProductName,
    string Barcode,
    int CurrentStock,
    int Threshold);
