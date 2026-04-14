namespace Wrapsfer.Mailing.Queue;

public sealed record MailRequestedMessage(
    Guid MailRequestId,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    IReadOnlyList<string> Bcc,
    string? Subject,
    string? BodyHtml,
    string? BodyText,
    string? TemplateName,
    IReadOnlyDictionary<string, string?>? Tokens,
    IReadOnlyList<SerializedAttachment> Attachments,
    DateTime QueuedAt);
