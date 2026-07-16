using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Entities;

/// <summary>
/// A raw Shopee push message persisted for idempotent, out-of-band processing.
/// Deduplicated by <see cref="MessageKey"/> (SHA-256 of the raw body, unique index),
/// mirroring the StripeWebhookEvent idempotency pattern.
/// </summary>
public sealed class ShopeeWebhookEvent : BaseEntity
{
    private const int MessageKeyMaxLength = 64;
    private const int ErrorMaxLength = 512;
    private static readonly TimeSpan s_initialBackoff = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan s_maxBackoff = TimeSpan.FromHours(1);

    private ShopeeWebhookEvent()
    {
    }

    public long ShopId { get; private set; }
    public int Code { get; private set; }
    public string MessageKey { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public ShopeeWebhookEventStatus Status { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public string? Error { get; private set; }

    public static Result<ShopeeWebhookEvent> Create(
        long shopId, int code, string messageKey, string payload, DateTime receivedAt)
    {
        if (shopId <= 0)
        {
            return Result<ShopeeWebhookEvent>.Failure(ShopeeWebhookEventErrors.InvalidShopId);
        }

        if (string.IsNullOrWhiteSpace(messageKey) || messageKey.Length > MessageKeyMaxLength)
        {
            return Result<ShopeeWebhookEvent>.Failure(ShopeeWebhookEventErrors.InvalidMessageKey);
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            return Result<ShopeeWebhookEvent>.Failure(ShopeeWebhookEventErrors.InvalidPayload);
        }

        ShopeeWebhookEvent webhookEvent = new()
        {
            ShopId = shopId,
            Code = code,
            MessageKey = messageKey.Trim(),
            Payload = payload,
            Status = ShopeeWebhookEventStatus.Pending,
            ReceivedAtUtc = receivedAt
        };

        return Result<ShopeeWebhookEvent>.Success(webhookEvent);
    }

    public void MarkProcessed(DateTime processedAt)
    {
        Status = ShopeeWebhookEventStatus.Processed;
        ProcessedAtUtc = processedAt;
        Error = null;
        NextAttemptAt = null;
    }

    public void MarkFailed(string error, DateTime attemptedAt, int maxAttempts)
    {
        Status = ShopeeWebhookEventStatus.Failed;
        AttemptCount++;
        Error = Truncate(error, ErrorMaxLength);

        if (AttemptCount >= maxAttempts)
        {
            NextAttemptAt = null;
            return;
        }

        double multiplier = Math.Pow(2, AttemptCount - 1);
        double minutes = Math.Min(s_initialBackoff.TotalMinutes * multiplier, s_maxBackoff.TotalMinutes);
        NextAttemptAt = attemptedAt.AddMinutes(minutes);
    }

    public void MarkIgnored()
    {
        Status = ShopeeWebhookEventStatus.Ignored;
        NextAttemptAt = null;
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
