namespace Wrapster.Mailing.Queue;

public sealed class MailQueueOptions
{
    public const string SectionName = "Mailing:Queue";

    public string QueueName { get; set; } = "wrapster.mail";
    public int MaxRetries { get; set; } = 3;
    public int InitialBackoffSeconds { get; set; } = 1;
}
