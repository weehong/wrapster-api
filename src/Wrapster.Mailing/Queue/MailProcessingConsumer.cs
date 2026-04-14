using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Wrapster.Mailing.Abstractions;
using Wrapster.Mailing.Resend;
using Wrapster.Mailing.Templates;

namespace Wrapster.Mailing.Queue;

public sealed class MailProcessingConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> rabbitOptions,
    IOptions<MailQueueOptions> queueOptions,
    ILogger<MailProcessingConsumer> logger) : BackgroundService
{
    private readonly MailQueueOptions _queueOptions = queueOptions.Value;
    private readonly RabbitMqOptions _rabbitOptions = rabbitOptions.Value;
    private readonly ResiliencePipeline<MailSendOutcome> _retryPolicy =
        MailRetryPolicy.Build(
            queueOptions.Value.MaxRetries,
            TimeSpan.FromSeconds(queueOptions.Value.InitialBackoffSeconds));
    private IChannel? _channel;
    private IConnection? _connection;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            ConnectionFactory factory = new()
            {
                HostName = _rabbitOptions.HostName,
                Port = _rabbitOptions.Port,
                UserName = _rabbitOptions.UserName,
                Password = _rabbitOptions.Password,
                VirtualHost = _rabbitOptions.VirtualHost
            };

            _connection = await factory.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await _channel.QueueDeclareAsync(
                _queueOptions.QueueName,
                true,
                false,
                false,
                cancellationToken: stoppingToken);

            await _channel.BasicQosAsync(0, 1, false, stoppingToken);

            AsyncEventingBasicConsumer consumer = new(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                ulong deliveryTag = ea.DeliveryTag;
                try
                {
                    string json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    MailRequestedMessage? envelope =
                        JsonSerializer.Deserialize<MailRequestedMessage>(json);

                    if (envelope is null)
                    {
                        logger.LogWarning("Received empty or malformed mail envelope; dropping");
                        await _channel.BasicNackAsync(deliveryTag, false, false, stoppingToken);
                        return;
                    }

                    bool handled = await HandleAsync(envelope, stoppingToken);
                    if (handled)
                    {
                        await _channel.BasicAckAsync(deliveryTag, false, stoppingToken);
                    }
                    else
                    {
                        await _channel.BasicNackAsync(deliveryTag, false, false, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unhandled exception while processing mail message; dropping");
                    await _channel.BasicNackAsync(deliveryTag, false, false, stoppingToken);
                }
            };

            await _channel.BasicConsumeAsync(
                _queueOptions.QueueName,
                false,
                consumer,
                stoppingToken);

            logger.LogInformation("Mail processing consumer started; listening on queue {Queue}",
                _queueOptions.QueueName);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Mail processing consumer terminated unexpectedly");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync(cancellationToken);
            _connection.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task<bool> HandleAsync(MailRequestedMessage envelope, CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ITemplateRenderer renderer = scope.ServiceProvider.GetRequiredService<ITemplateRenderer>();
        IFluentEmailMailSender sender = scope.ServiceProvider.GetRequiredService<IFluentEmailMailSender>();

        RenderedTemplate? rendered;
        if (!string.IsNullOrWhiteSpace(envelope.TemplateName))
        {
            IReadOnlyDictionary<string, object?>? tokens = envelope.Tokens?
                .ToDictionary(kv => kv.Key, kv => (object?)kv.Value);

            try
            {
                rendered = await renderer.RenderAsync(envelope.TemplateName, tokens, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to render template '{TemplateName}' for mail {MailRequestId}",
                    envelope.TemplateName, envelope.MailRequestId);
                return false;
            }

            if (rendered is null)
            {
                logger.LogError(
                    "Template '{TemplateName}' not found for mail {MailRequestId}; dropping",
                    envelope.TemplateName, envelope.MailRequestId);
                return false;
            }
        }
        else
        {
            rendered = new RenderedTemplate(envelope.Subject ?? string.Empty, envelope.BodyHtml, envelope.BodyText);
        }

        MailMessage message = new()
        {
            To = envelope.To,
            Cc = envelope.Cc,
            Bcc = envelope.Bcc,
            Subject = rendered.Subject,
            Attachments = envelope.Attachments
                .Select(a => new MailAttachment(a.FileName, a.Content, a.ContentType))
                .ToList()
        };

        MailSendOutcome outcome = await _retryPolicy.ExecuteAsync(
            async ct => await sender.SendAsync(message, rendered, ct),
            cancellationToken);

        if (outcome.IsSuccess)
        {
            logger.LogInformation(
                "Mail sent for request {MailRequestId} (template={TemplateName}, toCount={ToCount}, ccCount={CcCount}, bccCount={BccCount}, providerMessageId={ResendMessageId})",
                envelope.MailRequestId,
                envelope.TemplateName ?? "-",
                envelope.To.Count,
                envelope.Cc.Count,
                envelope.Bcc.Count,
                outcome.ProviderMessageId);
            return true;
        }

        logger.LogError(
            "Mail send failed for request {MailRequestId}: {ErrorCode} — {ErrorDescription} (transient={Transient})",
            envelope.MailRequestId, outcome.ErrorCode, outcome.ErrorDescription, outcome.IsTransient);

        return false;
    }
}
