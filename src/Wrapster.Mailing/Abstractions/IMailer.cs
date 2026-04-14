namespace Wrapster.Mailing.Abstractions;

public interface IMailer
{
    Task<MailRequestId> SendAsync(MailMessage message, CancellationToken cancellationToken = default);
}
