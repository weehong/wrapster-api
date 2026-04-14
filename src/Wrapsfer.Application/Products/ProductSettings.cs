namespace Wrapsfer.Application.Products;

public sealed class ProductSettings
{
    public const string SectionName = "ProductSettings";

    public int GlobalLowStockThreshold { get; set; } = 10;
}
