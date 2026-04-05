namespace Wrapster.Application.Abstractions.Email;

public interface IEmailTemplate
{
    string Subject { get; }
    IReadOnlyList<EmailAttachment>? Attachments => null;
    string RenderHtml();
}
