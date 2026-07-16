using System.Text.Json;
using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Services;

/// <summary>
/// Drains stored Shopee push events: order-status pushes flow through the ingestion
/// service; tracking-number pushes complete an arranged shipment. Failures retry with
/// backoff up to maxAttempts, after which the reconciliation job is the safety net.
/// </summary>
public sealed class ShopeeWebhookEventProcessor(
    IShopeeWebhookEventRepository eventRepository,
    IShopeeShopConnectionRepository connectionRepository,
    IShopeeOrderRepository orderRepository,
    IShopeeGateway shopeeGateway,
    ShopeeOrderIngestionService ingestionService,
    ShopeeOrderShipmentCompletionService completionService,
    ShopeeConnectionTokenRefresher tokenRefresher,
    IUnitOfWork unitOfWork,
    ILogger<ShopeeWebhookEventProcessor> logger)
{
    // Shopee Open Platform push message codes. Configurable constants so they can be
    // verified/adjusted against the Shopee Open Platform console during manual E2E —
    // Shopee does not publish a stable enum for these in its documentation.
    public const int OrderStatusPushCode = 3;
    public const int TrackingNumberPushCode = 4;

    public async Task<ShopeeWebhookRunSummary> RunAsync(
        int batchSize, int maxAttempts, CancellationToken cancellationToken)
    {
        IReadOnlyList<ShopeeWebhookEvent> events =
            await eventRepository.ListPendingAsync(DateTime.UtcNow, batchSize, cancellationToken);

        int processed = 0;
        int failed = 0;
        int ignored = 0;

        foreach (ShopeeWebhookEvent webhookEvent in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Result result;
            try
            {
                result = await ProcessAsync(webhookEvent, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Shopee webhook event {EventId} processing threw", webhookEvent.Id);
                result = Result.Failure(Error.Failure);
            }

            if (result.IsSuccess)
            {
                webhookEvent.MarkProcessed(DateTime.UtcNow);
                processed++;
                await unitOfWork.SaveChangesAsync(cancellationToken);
                continue;
            }

            // A failed/ignored result may follow a partial entity mutation (e.g. a cancellation
            // that marked the order Cancelled before stock release failed). Discard any such
            // half-applied work on the shared scoped context before recording the event outcome,
            // then reload the event fresh so its status update is the only pending change.
            unitOfWork.ClearChangeTracker();
            ShopeeWebhookEvent? freshEvent =
                await eventRepository.GetByIdAsync(webhookEvent.Id, cancellationToken);
            if (freshEvent is null)
            {
                logger.LogWarning(
                    "Shopee webhook event {EventId} could not be reloaded after a failed processing "
                    + "attempt; skipping status update", webhookEvent.Id);
                continue;
            }

            if (result.Error.Code == ShopeeOrderErrors.ConnectionNotFound.Code)
            {
                freshEvent.MarkIgnored();
                ignored++;
            }
            else
            {
                freshEvent.MarkFailed(result.Error.Code, DateTime.UtcNow, maxAttempts);
                failed++;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ShopeeWebhookRunSummary(processed, failed, ignored);
    }

    private async Task<Result> ProcessAsync(
        ShopeeWebhookEvent webhookEvent, CancellationToken cancellationToken)
    {
        ShopeeShopConnection? connection =
            await connectionRepository.GetByShopIdAsync(webhookEvent.ShopId, cancellationToken);
        if (connection is null)
        {
            return Result.Failure(ShopeeOrderErrors.ConnectionNotFound);
        }

        string? orderSn = ExtractOrderSn(webhookEvent.Payload);
        if (string.IsNullOrWhiteSpace(orderSn))
        {
            return Result.Failure(ShopeeWebhookEventErrors.MalformedPushBody);
        }

        await tokenRefresher.RefreshIfNeededAsync(connection, cancellationToken);

        if (webhookEvent.Code == OrderStatusPushCode)
        {
            return await ingestionService.IngestOrderAsync(connection, orderSn, cancellationToken);
        }

        return await CompleteTrackingAsync(connection, orderSn, webhookEvent.Payload, cancellationToken);
    }

    private async Task<Result> CompleteTrackingAsync(
        ShopeeShopConnection connection, string orderSn, string payload, CancellationToken cancellationToken)
    {
        ShopeeOrder? order = await orderRepository.GetByOrderSnAsync(
            orderSn, connection.TenantId, cancellationToken);
        if (order is null)
        {
            // Tracking push for an order we never ingested (e.g. arranged in Seller Centre).
            return await ingestionService.IngestOrderAsync(connection, orderSn, cancellationToken);
        }

        if (order.Status != ShopeeOrderStatus.AwaitingTracking)
        {
            return Result.Success();
        }

        string? trackingNumber = ExtractTrackingNumber(payload);
        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            Result<string?> trackingResult = await shopeeGateway.GetTrackingNumberAsync(
                connection.ShopId, connection.AccessToken, orderSn, cancellationToken);
            if (trackingResult.IsFailure)
            {
                return Result.Failure(trackingResult.Error);
            }

            trackingNumber = trackingResult.Value;
        }

        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            // Not assigned yet; the reconciliation job re-polls stuck orders.
            return Result.Success();
        }

        return await completionService.CompleteAsync(order, trackingNumber, cancellationToken);
    }

    private static string? ExtractOrderSn(string payload) =>
        ExtractDataString(payload, "ordersn") ?? ExtractDataString(payload, "order_sn");

    private static string? ExtractTrackingNumber(string payload) =>
        ExtractDataString(payload, "tracking_no") ?? ExtractDataString(payload, "tracking_number");

    private static string? ExtractDataString(string payload, string propertyName)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("data", out JsonElement data)
                && data.TryGetProperty(propertyName, out JsonElement value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
