using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions.Shopee;

namespace Wrapsfer.Infrastructure.Shopee;

internal sealed class ShopeeWebhookSignatureVerifier(
    IOptions<ShopeeOptions> options) : IShopeeWebhookSignatureVerifier
{
    public bool Verify(string? authorizationHeader, string requestBody)
    {
        ShopeeOptions shopeeOptions = options.Value;
        if (string.IsNullOrWhiteSpace(shopeeOptions.PartnerKey)
            || string.IsNullOrWhiteSpace(shopeeOptions.PushCallbackUrl)
            || string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return false;
        }

        string expected = ShopeeRequestSigner.SignPushCallback(
            shopeeOptions.PartnerKey, shopeeOptions.PushCallbackUrl, requestBody);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(authorizationHeader.Trim()));
    }
}
