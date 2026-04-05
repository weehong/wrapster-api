using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Resend;
using Wrapster.Application.Abstractions.Email;
using Wrapster.Domain.Common;
using EmailAttachment = Wrapster.Application.Abstractions.Email.EmailAttachment;
using Wrapster.Domain.Errors;

namespace Wrapster.Infrastructure.Email;

public sealed class ResendEmailService : IEmailService
{
    private readonly ILogger<ResendEmailService> _logger;
    private readonly ResendOptions _options;
    private readonly IResend _resendClient;

    public ResendEmailService(
        IResend resendClient,
        IOptions<ResendOptions> options,
        ILogger<ResendEmailService> logger)
    {
        _resendClient = resendClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result> SendAsync<TTemplate>(
        string to,
        TTemplate template,
        CancellationToken cancellationToken = default) where TTemplate : IEmailTemplate
    {
        try
        {
            string subject = template.Subject;
            string htmlBody = template.RenderHtml();
            IReadOnlyList<EmailAttachment>? attachments = template.Attachments;

            EmailMessage message = new()
            {
                From = $"{_options.FromName} <{_options.FromAddress}>",
                To = to,
                Subject = subject,
                HtmlBody = htmlBody
            };

            if (attachments is { Count: > 0 })
            {
                message.Attachments =
                [
                    ..attachments.Select(a => new Resend.EmailAttachment
                    {
                        Filename = a.FileName,
                        Content = a.Content,
                        ContentType = a.ContentType
                    })
                ];
            }

            _logger.LogInformation(
                "Sending email to {To} with subject '{Subject}' using template {TemplateName}",
                to, subject, typeof(TTemplate).Name);

            await _resendClient.EmailSendAsync(message, cancellationToken);

            _logger.LogInformation("Email sent successfully to {To}", to);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To} using template {TemplateName}",
                to, typeof(TTemplate).Name);

            return Result.Failure(EmailErrors.SendFailed);
        }
    }
}
