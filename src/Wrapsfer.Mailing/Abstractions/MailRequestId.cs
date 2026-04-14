namespace Wrapsfer.Mailing.Abstractions;

public readonly record struct MailRequestId(Guid Value)
{
    public static MailRequestId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
