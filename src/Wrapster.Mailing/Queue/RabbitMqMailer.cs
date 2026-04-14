using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Wrapster.Mailing.Abstractions;

namespace Wrapster.Mailing.Queue;

internal sealed class RabbitMqMailer(
    IOptions<RabbitMqOptions> rabbitOptions,
    IOptions<MailQueueOptions> queueOptions,
    ILogger<RabbitMqMailer> logger) : IMailer, IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly MailQueueOptions _queueOptions = queueOptions.Value;
    private readonly RabbitMqOptions _rabbitOptions = rabbitOptions.Value;
    private IChannel? _channel;
    private IConnection? _connection;

    public async Task<MailRequestId> SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        Validate(message);

        MailRequestId requestId = MailRequestId.New();

        MailRequestedMessage envelope = new(
            requestId.Value,
            message.To,
            message.Cc,
            message.Bcc,
            message.Subject,
            message.Body?.Html,
            message.Body?.Text,
            message.TemplateName,
            ConvertTokens(message.Tokens),
            message.Attachments
                .Select(a => new SerializedAttachment(a.FileName, a.ContentType, a.Content))
                .ToList(),
            DateTime.UtcNow);

        IChannel channel = await GetOrCreateChannelAsync(cancellationToken);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(envelope);

        BasicProperties properties = new()
        {
            Persistent = true,
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            MessageId = requestId.ToString()
        };

        await channel.BasicPublishAsync(
            string.Empty,
            _queueOptions.QueueName,
            false,
            properties,
            body,
            cancellationToken);

        logger.LogInformation(
            "Enqueued mail request {MailRequestId} to queue '{QueueName}' ({Size} bytes, template={TemplateName})",
            requestId, _queueOptions.QueueName, body.Length, message.TemplateName ?? "-");

        return requestId;
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

    private static void Validate(MailMessage message)
    {
        if (message.To is null || message.To.Count == 0)
        {
            throw new ArgumentException("MailMessage.To must contain at least one recipient.", nameof(message));
        }

        bool hasLiteral = message.Subject is not null && message.Body is not null;
        bool hasTemplate = !string.IsNullOrWhiteSpace(message.TemplateName);

        if (hasLiteral == hasTemplate)
        {
            throw new ArgumentException(
                "MailMessage must have exactly one of (Subject + Body) or (TemplateName).",
                nameof(message));
        }
    }

    private static IReadOnlyDictionary<string, string?>? ConvertTokens(
        IReadOnlyDictionary<string, object?>? tokens)
    {
        if (tokens is null)
        {
            return null;
        }

        Dictionary<string, string?> serialized = new(tokens.Count);
        foreach ((string key, object? value) in tokens)
        {
            serialized[key] = value?.ToString();
        }

        return serialized;
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
                    HostName = _rabbitOptions.HostName,
                    Port = _rabbitOptions.Port,
                    UserName = _rabbitOptions.UserName,
                    Password = _rabbitOptions.Password,
                    VirtualHost = _rabbitOptions.VirtualHost
                };

                _connection = await factory.CreateConnectionAsync(cancellationToken);
                logger.LogInformation(
                    "Connected to RabbitMQ at {HostName}:{Port} for mailing publisher",
                    _rabbitOptions.HostName, _rabbitOptions.Port);
            }

            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await _channel.QueueDeclareAsync(
                _queueOptions.QueueName,
                true,
                false,
                false,
                cancellationToken: cancellationToken);

            return _channel;
        }
        finally
        {
            _lock.Release();
        }
    }
}
