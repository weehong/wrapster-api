using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Commands.IngestShopeeWebhook;

internal sealed class IngestShopeeWebhookCommandHandler(
    IShopeeWebhookSignatureVerifier signatureVerifier,
    IShopeeWebhookEventRepository eventRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<IngestShopeeWebhookCommand>
{
    public async Task<Result> Handle(IngestShopeeWebhookCommand request, CancellationToken cancellationToken)
    {
        if (!signatureVerifier.Verify(request.AuthorizationHeader, request.Body))
        {
            return Result.Failure(ShopeeWebhookEventErrors.InvalidSignature);
        }

        int code;
        long shopId;
        try
        {
            using JsonDocument document = JsonDocument.Parse(request.Body);
            if (!document.RootElement.TryGetProperty("code", out JsonElement codeElement)
                || !document.RootElement.TryGetProperty("shop_id", out JsonElement shopIdElement))
            {
                return Result.Failure(ShopeeWebhookEventErrors.MalformedPushBody);
            }

            code = codeElement.GetInt32();
            shopId = shopIdElement.GetInt64();
        }
        catch (JsonException)
        {
            return Result.Failure(ShopeeWebhookEventErrors.MalformedPushBody);
        }

        string messageKey = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.Body)));

        bool duplicate = await eventRepository.ExistsByMessageKeyAsync(messageKey, cancellationToken);
        if (duplicate)
        {
            return Result.Success();
        }

        Result<ShopeeWebhookEvent> createResult = ShopeeWebhookEvent.Create(
            shopId, code, messageKey, request.Body, DateTime.UtcNow);
        if (createResult.IsFailure)
        {
            return Result.Failure(createResult.Error);
        }

        ShopeeWebhookEvent webhookEvent = createResult.Value;
        if (code is not (ShopeeWebhookEventProcessor.OrderStatusPushCode
            or ShopeeWebhookEventProcessor.TrackingNumberPushCode))
        {
            webhookEvent.MarkIgnored();
        }

        eventRepository.Add(webhookEvent);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
