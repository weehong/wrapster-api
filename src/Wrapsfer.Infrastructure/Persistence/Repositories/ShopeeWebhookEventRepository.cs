using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class ShopeeWebhookEventRepository(ApplicationDbContext context) : IShopeeWebhookEventRepository
{
    public async Task<bool> ExistsByMessageKeyAsync(
        string messageKey,
        CancellationToken cancellationToken = default) =>
        await context.ShopeeWebhookEvents
            .AnyAsync(e => e.MessageKey == messageKey, cancellationToken);

    public async Task<IReadOnlyList<ShopeeWebhookEvent>> ListPendingAsync(
        DateTime now,
        int batchSize,
        CancellationToken cancellationToken = default) =>
        await context.ShopeeWebhookEvents
            .Where(e => e.Status == ShopeeWebhookEventStatus.Pending
                        || (e.Status == ShopeeWebhookEventStatus.Failed
                            && e.NextAttemptAt != null
                            && e.NextAttemptAt <= now))
            .OrderBy(e => e.ReceivedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public void Add(ShopeeWebhookEvent webhookEvent) =>
        context.ShopeeWebhookEvents.Add(webhookEvent);
}
