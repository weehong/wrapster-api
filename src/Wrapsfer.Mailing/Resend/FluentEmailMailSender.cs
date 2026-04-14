using System.Net;
using FluentEmail.Core;
using FluentEmail.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapsfer.Mailing.Abstractions;
using Wrapsfer.Mailing.Errors;
using Wrapsfer.Mailing.Templates;

namespace Wrapsfer.Mailing.Resend;

internal sealed class FluentEmailMailSender(
    IFluentEmailFactory fluentEmailFactory,
    IOptions<ResendOptions> options,
    ILogger<FluentEmailMailSender> logger) : IFluentEmailMailSender
{
    private readonly ResendOptions _options = options.Value;

    public async Task<MailSendOutcome> SendAsync(
        MailMessage message,
        RenderedTemplate rendered,
        CancellationToken cancellationToken = default)
    {
        IFluentEmail email = fluentEmailFactory
            .Create()
            .SetFrom(_options.FromAddress, _options.FromName);

        foreach (string recipient in message.To)
        {
            email = email.To(recipient);
        }

        foreach (string recipient in message.Cc)
        {
            email = email.CC(recipient);
        }

        foreach (string recipient in message.Bcc)
        {
            email = email.BCC(recipient);
        }

        email = email.Subject(rendered.Subject);

        if (rendered.Html is not null)
        {
            email = email.Body(rendered.Html, isHtml: true);
            if (rendered.Text is not null)
            {
                email = email.PlaintextAlternativeBody(rendered.Text);
            }
        }
        else if (rendered.Text is not null)
        {
            email = email.Body(rendered.Text);
        }

        foreach (MailAttachment attachment in message.Attachments)
        {
            email = email.Attach(new Attachment
            {
                Filename = attachment.FileName,
                ContentType = attachment.ContentType,
                Data = new MemoryStream(attachment.Content)
            });
        }

        try
        {
            SendResponse response = await email.SendAsync(cancellationToken);

            if (response.Successful)
            {
                return MailSendOutcome.Success(response.MessageId);
            }

            string description = string.Join("; ", response.ErrorMessages ?? []);
            bool transient = IsTransient(description);
            logger.LogWarning(
                "FluentEmail reported send failure (transient={Transient}): {Errors}",
                transient, description);

            return transient
                ? MailSendOutcome.TransientFailure(MailingErrors.TransientSendFailure, description)
                : MailSendOutcome.PermanentFailure(MailingErrors.PermanentSendFailure, description);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Transient network failure while sending mail");
            return MailSendOutcome.TransientFailure(MailingErrors.TransientSendFailure, ex.Message);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Timeout while sending mail");
            return MailSendOutcome.TransientFailure(MailingErrors.TransientSendFailure, ex.Message);
        }
    }

    private static bool IsTransient(string errorDescription)
    {
        if (string.IsNullOrEmpty(errorDescription))
        {
            return false;
        }

        string[] transientMarkers =
        [
            nameof(HttpStatusCode.InternalServerError),
            nameof(HttpStatusCode.BadGateway),
            nameof(HttpStatusCode.ServiceUnavailable),
            nameof(HttpStatusCode.GatewayTimeout),
            nameof(HttpStatusCode.TooManyRequests),
            "500:", "502:", "503:", "504:", "429:"
        ];

        foreach (string marker in transientMarkers)
        {
            if (errorDescription.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
