using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Wrapster.Application.Abstractions.Queue;

namespace Wrapster.Infrastructure.Queue;

public sealed class RabbitMqMessagePublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<RabbitMqMessagePublisher> _logger;
    private readonly RabbitMqOptions _options;
    private IChannel? _channel;
    private IConnection? _connection;

    public RabbitMqMessagePublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqMessagePublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        _lock.Dispose();
    }

    public async Task PublishAsync<T>(
        string queueName,
        T message,
        CancellationToken cancellationToken = default) where T : class
    {
        IChannel channel = await GetOrCreateChannelAsync(cancellationToken);

        await channel.QueueDeclareAsync(
            queueName,
            true,
            false,
            false,
            cancellationToken: cancellationToken);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(message);

        BasicProperties properties = new()
        {
            Persistent = true,
            ContentType = "application/json",
            ContentEncoding = "utf-8"
        };

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            false,
            properties,
            body,
            cancellationToken);

        _logger.LogInformation(
            "Published message to queue '{QueueName}' (type: {MessageType}, size: {Size} bytes)",
            queueName, typeof(T).Name, body.Length);
    }

    private async Task<IChannel> GetOrCreateChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            if (_connection is null or { IsOpen: false })
            {
                ConnectionFactory factory = new()
                {
                    HostName = _options.HostName,
                    Port = _options.Port,
                    UserName = _options.UserName,
                    Password = _options.Password,
                    VirtualHost = _options.VirtualHost
                };

                _connection = await factory.CreateConnectionAsync(cancellationToken);
                _logger.LogInformation(
                    "Connected to RabbitMQ at {HostName}:{Port}",
                    _options.HostName, _options.Port);
            }

            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await _channel.BasicQosAsync(
                0,
                1,
                false,
                cancellationToken);

            return _channel;
        }
        finally
        {
            _lock.Release();
        }
    }
}
