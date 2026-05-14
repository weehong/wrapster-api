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

        services.AddHttpClient();

        services.AddFluentEmail("placeholder@wrapsfer.local")
            .AddResend();

        services.AddSingleton<ITemplateRenderer, FluidTemplateRenderer>();
        services.AddScoped<IFluentEmailMailSender, FluentEmailMailSender>();
        services.AddSingleton<IMailer, RabbitMqMailer>();
        services.AddHostedService<MailProcessingConsumer>();

        return services;
    }
}
