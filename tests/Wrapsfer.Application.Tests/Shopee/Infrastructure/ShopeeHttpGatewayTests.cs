using System.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Infrastructure.Shopee;

namespace Wrapsfer.Application.Tests.Shopee.Infrastructure;

public class ShopeeHttpGatewayTests
{
    private const string PartnerKey = "test-partner-key";
    private const long PartnerId = 843291;
    private const string BaseUrl = "https://partner.test-stable.shopeemobile.com";
    private const string RedirectUrl = "https://partner.wrapsfer.com/partner/shopee-callback";

    private readonly Mock<IHttpClientFactory> _httpClientFactory = new();

    private ShopeeHttpGateway CreateGateway(ShopeeOptions options) => new(
        _httpClientFactory.Object,
        Options.Create(options),
        NullLogger<ShopeeHttpGateway>.Instance);

    private static ShopeeOptions ConfiguredOptions() => new()
    {
        PartnerId = PartnerId,
        PartnerKey = PartnerKey,
        BaseUrl = BaseUrl
    };

    [Fact]
    public void BuildShopAuthorizationUrl_WhenConfigured_BuildsSignedAuthPartnerUrl()
    {
        ShopeeHttpGateway gateway = CreateGateway(ConfiguredOptions());

        Result<string> result = gateway.BuildShopAuthorizationUrl(RedirectUrl);

        result.IsSuccess.Should().BeTrue();
        Uri uri = new(result.Value);
        uri.GetLeftPart(UriPartial.Path).Should().Be($"{BaseUrl}/api/v2/shop/auth_partner");

        System.Collections.Specialized.NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);
        query["partner_id"].Should().Be(PartnerId.ToString());
        query["redirect"].Should().Be(RedirectUrl);
        long timestamp = long.Parse(query["timestamp"]!);
        query["sign"].Should().Be(ShopeeRequestSigner.SignPublicRequest(
            PartnerKey, PartnerId, "/api/v2/shop/auth_partner", timestamp));
    }

    [Fact]
    public void BuildShopAuthorizationUrl_WhenNotConfigured_FailsWithNotConfigured()
    {
        ShopeeHttpGateway gateway = CreateGateway(new ShopeeOptions());

        Result<string> result = gateway.BuildShopAuthorizationUrl(RedirectUrl);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeShopConnectionErrors.NotConfigured);
    }

    [Fact]
    public void BuildShopAuthorizationUrl_WhenRedirectNotAbsoluteHttp_Fails()
    {
        ShopeeHttpGateway gateway = CreateGateway(ConfiguredOptions());

        Result<string> result = gateway.BuildShopAuthorizationUrl("javascript:alert(1)");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeShopConnectionErrors.InvalidRedirectUrl);
    }
}
