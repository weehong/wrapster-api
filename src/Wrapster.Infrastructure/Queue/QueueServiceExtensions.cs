using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wrapster.Application.Abstractions.Queue;

namespace Wrapster.Infrastructure.Queue;

public static class QueueServiceExtensions
{
    public static IServiceCollection AddQueueService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(RabbitMqOptions.SectionName);

        services.AddOptions<RabbitMqOptions>()
            .Bind(section)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.HostName),
                "RabbitMq:HostName must be configured.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.UserName),
                "RabbitMq:UserName must be configured.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Password),
                "RabbitMq:Password must be configured.")
            .ValidateOnStart();

        services.AddSingleton<RabbitMqMessagePublisher>();
        services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<RabbitMqMessagePublisher>());

        return services;
    }
}
