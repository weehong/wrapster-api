namespace Wrapster.Mailing.Queue;

public sealed record SerializedAttachment(string FileName, string ContentType, byte[] Content);
