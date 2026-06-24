using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Domain.Repositories;

public interface IStripeWebhookEventRepository
{
    Task<bool> ExistsAsync(string stripeEventId, CancellationToken cancellationToken = default);

    void Add(StripeWebhookEvent webhookEvent);
}
