using System.Security.Cryptography;
using System.Text;

namespace Wrapsfer.Infrastructure.Shopee;

/// <summary>
/// Computes Shopee Open Platform v2 request signatures: lowercase hex
/// HMAC-SHA256 over the concatenated base string, keyed by the partner key.
/// </summary>
internal static class ShopeeRequestSigner
{
    /// <summary>Public APIs (auth): base string is {partner_id}{api_path}{timestamp}.</summary>
    public static string SignPublicRequest(string partnerKey, long partnerId, string apiPath, long timestamp) =>
        Sign(partnerKey, $"{partnerId}{apiPath}{timestamp}");

    /// <summary>Shop APIs: base string is {partner_id}{api_path}{timestamp}{access_token}{shop_id}.</summary>
    public static string SignShopRequest(
        string partnerKey,
        long partnerId,
        string apiPath,
        long timestamp,
        string accessToken,
        long shopId) =>
        Sign(partnerKey, $"{partnerId}{apiPath}{timestamp}{accessToken}{shopId}");

    private static string Sign(string partnerKey, string baseString)
    {
        byte[] hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(partnerKey),
            Encoding.UTF8.GetBytes(baseString));
        return Convert.ToHexStringLower(hash);
    }
}
