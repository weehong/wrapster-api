namespace Wrapster.Mailing.Abstractions;

public sealed record MailAttachment(string FileName, byte[] Content, string ContentType);
