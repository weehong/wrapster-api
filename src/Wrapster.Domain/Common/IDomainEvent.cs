namespace Wrapster.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
