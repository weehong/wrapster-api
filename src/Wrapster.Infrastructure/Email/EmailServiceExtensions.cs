using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Resend;
using Wrapster.Application.Abstractions.Email;

namespace Wrapster.Infrastructure.Email;

public static class EmailServiceExtensions
{
    public static IServiceCollection AddEmailService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(ResendOptions.SectionName);

        services.AddOptions<ResendOptions>()
            .Bind(section)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ApiKey),
                "Resend API key must be configured.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.FromAddress),
                "Resend FromAddress must be configured.")
            .ValidateOnStart();

        services.AddOptions<ResendClientOptions>()
            .Configure<IOptions<ResendOptions>>((clientOptions, resendOptions) =>
            {
                clientOptions.ApiToken = resendOptions.Value.ApiKey;
            });

        services.AddHttpClient<IResend, ResendClient>();
        services.AddScoped<IEmailService, ResendEmailService>();

        return services;
    }
}
