using Wrapsfer.Mailing.Abstractions;
using Wrapsfer.Mailing.Templates;

namespace Wrapsfer.Mailing.Resend;

internal interface IFluentEmailMailSender
{
    Task<MailSendOutcome> SendAsync(
        MailMessage message,
        RenderedTemplate rendered,
        CancellationToken cancellationToken = default);
}
