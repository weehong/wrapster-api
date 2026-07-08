using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Entities;

public sealed class ShopeeProductLink : AuditableEntity
{
    private const int ItemNameMaxLength = 256;
    private const int ModelNameMaxLength = 256;
    private const int SkuMaxLength = 128;
    private const int ErrorMaxLength = 512;
    private static readonly TimeSpan s_initialBackoff = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan s_maxBackoff = TimeSpan.FromHours(6);

    private ShopeeProductLink()
    {
    }

    public string TenantId { get; private set; } = default!;
    public Guid ProductId { get; private set; }
    public long ShopeeItemId { get; private set; }
    public long ShopeeModelId { get; private set; }
    public string? ShopeeItemName { get; private set; }
    public string? ShopeeModelName { get; private set; }
    public string? ShopeeItemSku { get; private set; }
    public int? LastSyncedQuantity { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }
    public DateTime? LastSyncAttemptedAt { get; private set; }
    public int SyncFailureCount { get; private set; }
    public DateTime? NextSyncEligibleAt { get; private set; }
    public string? LastSyncError { get; private set; }
    public string? LinkedBy { get; private set; }

    public static Result<ShopeeProductLink> Create(
        string tenantId,
        Guid productId,
        long shopeeItemId,
        long shopeeModelId,
        string? shopeeItemName,
        string? shopeeModelName,
        string? shopeeItemSku,
        string? linkedBy)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<ShopeeProductLink>.Failure(ShopeeProductLinkErrors.InvalidTenantId);
        }

        if (productId == Guid.Empty)
        {
            return Result<ShopeeProductLink>.Failure(ShopeeProductLinkErrors.ProductNotFound);
        }

        if (shopeeItemId <= 0)
        {
            return Result<ShopeeProductLink>.Failure(ShopeeProductLinkErrors.InvalidItemId);
        }

        if (shopeeModelId < 0)
        {
            return Result<ShopeeProductLink>.Failure(ShopeeProductLinkErrors.InvalidModelId);
        }

        ShopeeProductLink link = new()
        {
            TenantId = tenantId.Trim(),
            ProductId = productId,
            ShopeeItemId = shopeeItemId,
            ShopeeModelId = shopeeModelId,
            ShopeeItemName = Truncate(shopeeItemName, ItemNameMaxLength),
            ShopeeModelName = Truncate(shopeeModelName, ModelNameMaxLength),
            ShopeeItemSku = Truncate(shopeeItemSku, SkuMaxLength),
            LinkedBy = Truncate(linkedBy, 256)
        };

        return Result<ShopeeProductLink>.Success(link);
    }

    public void MarkSynced(int quantity, DateTime syncedAt)
    {
        LastSyncedQuantity = Math.Max(quantity, 0);
        LastSyncedAt = syncedAt;
        LastSyncAttemptedAt = syncedAt;
        LastSyncError = null;
        SyncFailureCount = 0;
        NextSyncEligibleAt = null;
    }

    public void MarkSyncFailed(string error, DateTime attemptedAt)
    {
        LastSyncAttemptedAt = attemptedAt;
        SyncFailureCount++;
        LastSyncError = Truncate(error, ErrorMaxLength);

        double multiplier = Math.Pow(2, SyncFailureCount - 1);
        double minutes = Math.Min(s_initialBackoff.TotalMinutes * multiplier, s_maxBackoff.TotalMinutes);
        NextSyncEligibleAt = attemptedAt.AddMinutes(minutes);
    }

    public void UpdateShopeeSnapshot(string? itemName, string? modelName, string? sku)
    {
        ShopeeItemName = Truncate(itemName, ItemNameMaxLength);
        ShopeeModelName = Truncate(modelName, ModelNameMaxLength);
        ShopeeItemSku = Truncate(sku, SkuMaxLength);
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
