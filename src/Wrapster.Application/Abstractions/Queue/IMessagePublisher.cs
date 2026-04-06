namespace Wrapster.Application.Abstractions.Queue;

public interface IMessagePublisher
{
    Task PublishAsync<T>(string queueName, T message, CancellationToken cancellationToken = default)
        where T : class;
}
