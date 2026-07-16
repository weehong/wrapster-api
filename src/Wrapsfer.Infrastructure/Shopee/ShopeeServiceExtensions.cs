using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wrapsfer.Application.Abstractions.Shopee;

namespace Wrapsfer.Infrastructure.Shopee;

public static class ShopeeServiceExtensions
{
    private static readonly TimeSpan s_httpTimeout = TimeSpan.FromSeconds(30);

    public static IServiceCollection AddShopeeIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ShopeeOptions>()
            .Bind(configuration.GetSection(ShopeeOptions.SectionName))
            .ValidateOnStart();

        // No BaseAddress: the gateway builds absolute URLs from ShopeeOptions.BaseUrl so an
        // unconfigured integration degrades to a Result failure instead of a startup crash.
        services.AddHttpClient(ShopeeHttpGateway.HttpClientName, client => client.Timeout = s_httpTimeout);

        services.AddSingleton<IShopeeGateway, ShopeeHttpGateway>();
        services.AddScoped<IShopeeWebhookSignatureVerifier, ShopeeWebhookSignatureVerifier>();

        return services;
    }
}
