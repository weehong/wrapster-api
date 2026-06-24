using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wrapsfer.Application.Billing;
using Wrapsfer.Application.Billing.Abstractions;

namespace Wrapsfer.Infrastructure.Billing;

public static class BillingServiceExtensions
{
    public static IServiceCollection AddStripeBilling(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<StripeOptions>()
            .Bind(configuration.GetSection(StripeOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IStripeBillingGateway, StripeBillingGateway>();

        return services;
    }
}
