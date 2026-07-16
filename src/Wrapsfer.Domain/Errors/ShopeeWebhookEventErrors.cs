using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Errors;

public static class ShopeeWebhookEventErrors
{
    public static readonly Error InvalidShopId = new(
        "ShopeeWebhookEvent.InvalidShopId", "Shop ID must be a positive number", ErrorType.Validation);

    public static readonly Error InvalidMessageKey = new(
        "ShopeeWebhookEvent.InvalidMessageKey", "Message key is required", ErrorType.Validation);

    public static readonly Error InvalidPayload = new(
        "ShopeeWebhookEvent.InvalidPayload", "Payload is required", ErrorType.Validation);

    public static readonly Error InvalidSignature = new(
        "ShopeeWebhookEvent.InvalidSignature", "The push signature is invalid", ErrorType.Validation);

    public static readonly Error MalformedPushBody = new(
        "ShopeeWebhookEvent.MalformedPushBody",
        "The push body is not valid Shopee push JSON",
        ErrorType.Validation);
}
