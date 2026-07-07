using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Resend;
using Wrapsfer.Mailing.Abstractions;
using Wrapsfer.Mailing.Queue;
using Wrapsfer.Mailing.Resend;
using Wrapsfer.Mailing.Templates;

namespace Wrapsfer.Mailing;

public static class DependencyInjection
{
    public static IServiceCollection AddMailing(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ResendOptions>()
            .Bind(configuration.GetSection(ResendOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "Mailing:Resend:ApiKey is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.FromAddress), "Mailing:Resend:FromAddress is required.")
            .ValidateOnStart();

        services.AddOptions<MailQueueOptions>()
            .Bind(configuration.GetSection(MailQueueOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.HostName), "RabbitMq:HostName is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.UserName), "RabbitMq:UserName is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Password), "RabbitMq:Password is required.")
            .ValidateOnStart();

        services.AddOptions<ResendClientOptions>()
            .Configure<IOptions<ResendOptions>>((clientOptions, resendOptions) =>
                clientOptions.ApiToken = resendOptions.Value.ApiKey);

        // Resend.FluentEmail's AddResend() wires ISender as a singleton that transitively needs
        // IOptionsSnapshot<ResendClientOptions>. The default IOptionsSnapshot registration is scoped,
        // which can't be resolved from a singleton at the root provider — every send throws.
        // Bridge it with a singleton adapter that exposes IOptions<T> as IOptionsSnapshot<T>;
        // reload-on-scope semantics (which we don't use for the Resend API token) are deliberately dropped.
        services.AddSingleton<IOptionsSnapshot<ResendClientOptions>>(sp =>
            new SingletonOptionsSnapshot<ResendClientOptions>(
                sp.GetRequiredService<IOptions<ResendClientOptions>>()));

        services.AddHttpClient();

        // The default-from address is a fallback FluentEmail uses if a caller doesn't call SetFrom.
        // We always set From explicitly in FluentEmailMailSender, but keep this on a verified
        // wrapsfer.com sender so a missing/misconfigured ResendOptions.FromAddress can't ever
        // cause Resend to reject the message on an unverified-domain error.
        services.AddFluentEmail("noreply@wrapsfer.com")
            .AddResend();

        services.AddSingleton<ITemplateRenderer, FluidTemplateRenderer>();
        services.AddScoped<IFluentEmailMailSender, FluentEmailMailSender>();
        services.AddSingleton<IMailer, RabbitMqMailer>();
        services.AddHostedService<MailProcessingConsumer>();

        return services;
    }
}
