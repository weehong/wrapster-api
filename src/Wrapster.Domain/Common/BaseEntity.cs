namespace Wrapster.Domain.Common;

public abstract class BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public Guid Id { get; private init; } = Guid.NewGuid();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void RemoveDomainEvent(IDomainEvent domainEvent) => _domainEvents.Remove(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
