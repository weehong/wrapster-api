using Wrapster.Mailing.Abstractions;
using Wrapster.Mailing.Templates;

namespace Wrapster.Mailing.Resend;

internal interface IFluentEmailMailSender
{
    Task<MailSendOutcome> SendAsync(
        MailMessage message,
        RenderedTemplate rendered,
        CancellationToken cancellationToken = default);
}
