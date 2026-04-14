namespace Wrapsfer.Infrastructure.Queue;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; init; } = string.Empty;
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string VirtualHost { get; init; } = "/";
}
