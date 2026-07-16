namespace Wrapsfer.Application.Abstractions.Shopee;

public interface IShopeeWebhookSignatureVerifier
{
    bool Verify(string? authorizationHeader, string requestBody);
}
