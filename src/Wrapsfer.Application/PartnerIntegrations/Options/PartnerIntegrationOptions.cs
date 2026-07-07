namespace Wrapsfer.Application.PartnerIntegrations.Options;

public sealed class PartnerIntegrationOptions
{
    public const string SectionName = "PartnerIntegrations";

    public string ApiBaseUrl { get; set; } = string.Empty;
}
