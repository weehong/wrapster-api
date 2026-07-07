namespace Wrapsfer.Application.Billing;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    /// <summary>Stripe secret API key. Provisioned via secrets; never committed.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Signing secret used to verify inbound webhook signatures.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>ISO currency code in Stripe's lowercase form, e.g. "myr".</summary>
    public string Currency { get; set; } = "myr";

    /// <summary>Full monthly stock report price in the currency's minor units (e.g. sen for MYR).</summary>
    public int StockReportMonthlyAmountMinor { get; set; } = 1000;

    /// <summary>IANA time zone whose calendar month defines the billing window.</summary>
    public string BillingTimeZone { get; set; } = "Asia/Singapore";

    /// <summary>Base URL of the partner frontend, used to build checkout and portal return URLs.</summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;

    public string CheckoutSuccessPath { get; set; } = "/partner/billing?checkout=success";

    public string CheckoutCancelPath { get; set; } = "/partner/billing?checkout=cancel";

    public string BillingPortalReturnPath { get; set; } = "/partner/billing";
}
