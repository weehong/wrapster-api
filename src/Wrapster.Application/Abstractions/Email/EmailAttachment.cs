namespace Wrapster.Application.Abstractions.Email;

public sealed record EmailAttachment(string FileName, byte[] Content, string ContentType);
