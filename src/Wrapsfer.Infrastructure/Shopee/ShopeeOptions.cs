namespace Wrapsfer.Infrastructure.Shopee;

public sealed class ShopeeOptions
{
    public const string SectionName = "Shopee";

    /// <summary>Numeric Shopee Open Platform partner ID for the Wrapsfer app. Not a secret.</summary>
    public long PartnerId { get; set; }

    /// <summary>Shopee partner key used to sign every request. Provisioned via Infisical (SHOPEE_PARTNER_KEY); never committed.</summary>
    public string PartnerKey { get; set; } = string.Empty;

    /// <summary>Shopee Open Platform host. Production: https://partner.shopeemobile.com; sandbox: https://partner.test-stable.shopeemobile.com.</summary>
    public string BaseUrl { get; set; } = "https://partner.shopeemobile.com";

    /// <summary>
    /// Partner frontend origin the authorization callback relays the browser to. Shopee only
    /// redirects to the domain registered in its console — the API's public domain — so the API
    /// receives the redirect and forwards code/shop_id to the frontend callback page.
    /// </summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;

    /// <summary>Frontend route that completes the authorization with the relayed code and shop_id.</summary>
    public string AuthorizationCallbackPath { get; set; } = "/partner/shopee-callback";

    public bool IsConfigured =>
        PartnerId > 0
        && !string.IsNullOrWhiteSpace(PartnerKey)
        && Uri.TryCreate(BaseUrl, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
