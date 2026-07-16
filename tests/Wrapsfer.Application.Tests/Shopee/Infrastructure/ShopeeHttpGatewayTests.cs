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

    private ShopeeHttpGateway CreateGateway(ShopeeOptions options, QueueHttpMessageHandler handler)
    {
        HttpClient client = new(handler);
        _httpClientFactory.Setup(f => f.CreateClient(ShopeeHttpGateway.HttpClientName)).Returns(client);
        return CreateGateway(options);
    }

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

    [Fact]
    public async Task GetItemListAsync_BuildsSignedQuery()
    {
        QueueHttpMessageHandler handler = new();
        handler.EnqueueJson("""
        {"error":"","response":{"item":[{"item_id":1001}],"has_next_page":true,"next_offset":20,"total_count":30}}
        """);
        ShopeeHttpGateway gateway = CreateGateway(ConfiguredOptions(), handler);

        Result<Wrapsfer.Application.Abstractions.Shopee.ShopeeItemPage> result =
            await gateway.GetItemListAsync(123456, "access-token", 0, 20, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Uri uri = handler.Requests[0].RequestUri!;
        uri.GetLeftPart(UriPartial.Path).Should().Be($"{BaseUrl}/api/v2/product/get_item_list");
        System.Collections.Specialized.NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);
        query["partner_id"].Should().Be(PartnerId.ToString());
        query["access_token"].Should().Be("access-token");
        query["shop_id"].Should().Be("123456");
        query["offset"].Should().Be("0");
        query["page_size"].Should().Be("20");
        query["item_status"].Should().Be("NORMAL");
        long timestamp = long.Parse(query["timestamp"]!);
        query["sign"].Should().Be(ShopeeRequestSigner.SignShopRequest(
            PartnerKey, PartnerId, "/api/v2/product/get_item_list", timestamp, "access-token", 123456));
    }

    [Fact]
    public async Task GetItemBaseInfoAsync_ChunksItemIdsByFifty()
    {
        QueueHttpMessageHandler handler = new();
        handler.EnqueueJson("""
        {"error":"","response":{"item_list":[]}}
        """);
        handler.EnqueueJson("""
        {"error":"","response":{"item_list":[]}}
        """);
        ShopeeHttpGateway gateway = CreateGateway(ConfiguredOptions(), handler);
        List<long> itemIds = Enumerable.Range(1, 51).Select(i => (long)i).ToList();

        Result<IReadOnlyList<Wrapsfer.Application.Abstractions.Shopee.ShopeeItemDetail>> result =
            await gateway.GetItemBaseInfoAsync(123456, "access-token", itemIds, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        handler.Requests.Should().HaveCount(2);
        HttpUtility.ParseQueryString(handler.Requests[0].RequestUri!.Query)["item_id_list"]!
            .Split(',').Should().HaveCount(50);
        HttpUtility.ParseQueryString(handler.Requests[1].RequestUri!.Query)["item_id_list"]
            .Should().Be("51");
    }

    [Fact]
    public async Task UpdateStockAsync_SendsDefaultLocationStockBody()
    {
        QueueHttpMessageHandler handler = new();
        handler.EnqueueJson("""{"error":"","message":"","request_id":"abc"}""");
        ShopeeHttpGateway gateway = CreateGateway(ConfiguredOptions(), handler);

        Result result = await gateway.UpdateStockAsync(
            123456, "access-token", 1001, 2002, 7, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        string body = await handler.Requests[0].Content!.ReadAsStringAsync();
        body.Should().Contain("\"item_id\":1001");
        body.Should().Contain("\"model_id\":2002");
        body.Should().Contain("\"seller_stock\":[{\"stock\":7}]");
        body.Should().NotContain("location_id");
    }

    [Fact]
    public async Task UpdateStockAsync_WhenEnvelopeError_ReturnsStockPushFailed()
    {
        QueueHttpMessageHandler handler = new();
        handler.EnqueueJson("""{"error":"error_param","message":"bad"}""");
        ShopeeHttpGateway gateway = CreateGateway(ConfiguredOptions(), handler);

        Result result = await gateway.UpdateStockAsync(
            123456, "access-token", 1001, 0, 7, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeProductLinkErrors.StockPushFailed);
    }

    [Fact]
    public async Task UpdateStockAsync_WhenAuthEnvelopeError_ReturnsAuthFailed()
    {
        QueueHttpMessageHandler handler = new();
        handler.EnqueueJson("""{"error":"invalid_access_token","message":"expired"}""");
        ShopeeHttpGateway gateway = CreateGateway(ConfiguredOptions(), handler);

        Result result = await gateway.UpdateStockAsync(
            123456, "access-token", 1001, 0, 7, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeProductLinkErrors.AuthFailed);
    }

    [Fact]
    public async Task GetModelListAsync_WhenHttpFails_ReturnsItemFetchFailed()
    {
        QueueHttpMessageHandler handler = new();
        handler.EnqueueJson("""{"error":"server"}""", System.Net.HttpStatusCode.InternalServerError);
        ShopeeHttpGateway gateway = CreateGateway(ConfiguredOptions(), handler);

        Result<IReadOnlyList<Wrapsfer.Application.Abstractions.Shopee.ShopeeItemModel>> result =
            await gateway.GetModelListAsync(123456, "access-token", 1001, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeProductLinkErrors.ItemFetchFailed);
    }

    [Fact]
    public async Task DownloadShippingDocumentAsync_WhenCreateAndDownloadSucceed_ReturnsPdfBytes()
    {
        byte[] pdfBytes = "%PDF-1.4 fake label"u8.ToArray();
        QueueHttpMessageHandler handler = new();
        handler.EnqueueJson("""{"error":"","message":"","request_id":"abc"}""");
        handler.EnqueueBytes(pdfBytes);
        ShopeeHttpGateway gateway = CreateGateway(ConfiguredOptions(), handler);

        Result<byte[]> result = await gateway.DownloadShippingDocumentAsync(
            123456, "access-token", "order-sn-1", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(pdfBytes);
    }

    [Fact]
    public async Task DownloadShippingDocumentAsync_WhenDocumentAlreadyExists_StillDownloadsPdfBytes()
    {
        byte[] pdfBytes = "%PDF-1.4 fake label"u8.ToArray();
        QueueHttpMessageHandler handler = new();
        handler.EnqueueJson("""{"error":"logistics.shipping_document_exist","message":"already created"}""");
        handler.EnqueueBytes(pdfBytes);
        ShopeeHttpGateway gateway = CreateGateway(ConfiguredOptions(), handler);

        Result<byte[]> result = await gateway.DownloadShippingDocumentAsync(
            123456, "access-token", "order-sn-1", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(pdfBytes);
    }

    [Fact]
    public async Task DownloadShippingDocumentAsync_WhenDownloadReturnsJsonErrorBody_ReturnsLabelFetchFailed()
    {
        QueueHttpMessageHandler handler = new();
        handler.EnqueueJson("""{"error":"","message":"","request_id":"abc"}""");
        handler.EnqueueJson("""{"error":"logistics.document_not_ready","message":"not ready yet"}""");
        ShopeeHttpGateway gateway = CreateGateway(ConfiguredOptions(), handler);

        Result<byte[]> result = await gateway.DownloadShippingDocumentAsync(
            123456, "access-token", "order-sn-1", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeOrderErrors.LabelFetchFailed);
    }
}
