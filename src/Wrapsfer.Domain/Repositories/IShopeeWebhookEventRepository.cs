using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Domain.Repositories;

public interface IShopeeWebhookEventRepository
{
    Task<bool> ExistsByMessageKeyAsync(string messageKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists events ready for processing: Pending, or Failed with a due retry. Spans all
    /// shops/tenants by design — used only by the webhook dispatch background job.
    /// </summary>
    Task<IReadOnlyList<ShopeeWebhookEvent>> ListPendingAsync(
        DateTime now, int batchSize, CancellationToken cancellationToken = default);

    Task<ShopeeWebhookEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(ShopeeWebhookEvent webhookEvent);
}
