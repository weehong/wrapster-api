using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class StripeWebhookEventRepository(ApplicationDbContext context) : IStripeWebhookEventRepository
{
    public async Task<bool> ExistsAsync(string stripeEventId, CancellationToken cancellationToken = default) =>
        await context.StripeWebhookEvents
            .AnyAsync(e => e.StripeEventId == stripeEventId, cancellationToken);

    public void Add(StripeWebhookEvent webhookEvent) => context.StripeWebhookEvents.Add(webhookEvent);
}
