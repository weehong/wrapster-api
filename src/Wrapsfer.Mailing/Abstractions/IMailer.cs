namespace Wrapsfer.Mailing.Abstractions;

public interface IMailer
{
    Task<MailRequestId> SendAsync(MailMessage message, CancellationToken cancellationToken = default);
}
