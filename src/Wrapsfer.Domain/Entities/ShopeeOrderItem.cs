using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Entities;

public sealed class ShopeeOrderItem : BaseEntity
{
    private const int NameMaxLength = 256;
    private const int SkuMaxLength = 128;

    private ShopeeOrderItem()
    {
    }

    public string TenantId { get; private set; } = default!;
    public Guid ShopeeOrderId { get; private set; }
    public long ShopeeItemId { get; private set; }
    public long ShopeeModelId { get; private set; }
    public string? ItemName { get; private set; }
    public string? ModelName { get; private set; }
    public string? ItemSku { get; private set; }
    public int Quantity { get; private set; }
    public Guid? ProductId { get; private set; }

    internal static ShopeeOrderItem FromSnapshot(
        string tenantId, Guid shopeeOrderId, ShopeeOrderItemSnapshot snapshot)
    {
        return new ShopeeOrderItem
        {
            TenantId = tenantId,
            ShopeeOrderId = shopeeOrderId,
            ShopeeItemId = snapshot.ShopeeItemId,
            ShopeeModelId = snapshot.ShopeeModelId,
            ItemName = Truncate(snapshot.ItemName, NameMaxLength),
            ModelName = Truncate(snapshot.ModelName, NameMaxLength),
            ItemSku = Truncate(snapshot.ItemSku, SkuMaxLength),
            Quantity = snapshot.Quantity,
            ProductId = snapshot.ProductId
        };
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
