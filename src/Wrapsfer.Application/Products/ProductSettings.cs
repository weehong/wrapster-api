namespace Wrapsfer.Application.Products;

public sealed class ProductSettings
{
    public const string SectionName = "ProductSettings";

    public int GlobalLowStockThreshold { get; set; } = 10;

    public int LowStockReminderIntervalHours { get; set; } = 168;

    public int MaxLowStockAlerts { get; set; } = 3;
}
