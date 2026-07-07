using Wrapsfer.Infrastructure.Shopee;

namespace Wrapsfer.Application.Tests.Shopee.Infrastructure;

public class ShopeeRequestSignerTests
{
    private const string PartnerKey = "test-partner-key";
    private const long PartnerId = 843291;
    private const long Timestamp = 1751788800;

    // Expected values computed independently with Python:
    // hmac.new(key, base_string, hashlib.sha256).hexdigest()

    [Fact]
    public void SignPublicRequest_ProducesLowercaseHexHmacOverPartnerIdPathTimestamp()
    {
        string sign = ShopeeRequestSigner.SignPublicRequest(
            PartnerKey, PartnerId, "/api/v2/shop/auth_partner", Timestamp);

        sign.Should().Be("688165c85c77eb3695aa8601e5424d50ef52f70b9c08e9814691a9831f1d879b");
    }

    [Fact]
    public void SignShopRequest_IncludesAccessTokenAndShopIdInBaseString()
    {
        string sign = ShopeeRequestSigner.SignShopRequest(
            PartnerKey, PartnerId, "/api/v2/shop/get_shop_info", Timestamp, "access-token", 123456);

        sign.Should().Be("c12cb0a391ff16e6d21004f4af72beb020a2b9b887a4132813a3dcb85311733a");
    }
}
