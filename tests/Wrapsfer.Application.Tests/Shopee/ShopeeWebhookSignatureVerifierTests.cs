using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Wrapsfer.Infrastructure.Shopee;

namespace Wrapsfer.Application.Tests.Shopee;

public sealed class ShopeeWebhookSignatureVerifierTests
{
    private const string Url = "https://api.wrapsfer.com/api/v1/shopee/webhook";
    private const string Key = "test-partner-key";

    private static ShopeeWebhookSignatureVerifier CreateVerifier() =>
        new(Options.Create(new ShopeeOptions
        {
            PartnerId = 1001,
            PartnerKey = Key,
            PushCallbackUrl = Url
        }));

    private static string Sign(string body)
    {
        byte[] hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Key), Encoding.UTF8.GetBytes($"{Url}|{body}"));
        return Convert.ToHexStringLower(hash);
    }

    [Fact]
    public void Verify_WithValidSignature_ReturnsTrue()
    {
        string body = "{\"code\":3,\"shop_id\":123}";
        CreateVerifier().Verify(Sign(body), body).Should().BeTrue();
    }

    [Fact]
    public void Verify_WithWrongSignature_ReturnsFalse()
    {
        CreateVerifier().Verify("deadbeef", "{\"code\":3}").Should().BeFalse();
    }

    [Fact]
    public void Verify_WithMissingHeader_ReturnsFalse()
    {
        CreateVerifier().Verify(null, "{\"code\":3}").Should().BeFalse();
    }
}
