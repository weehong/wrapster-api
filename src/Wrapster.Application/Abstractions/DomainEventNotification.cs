using MediatR;
using Wrapster.Domain.Common;

namespace Wrapster.Application.Abstractions;

public sealed class DomainEventNotification<TDomainEvent>(TDomainEvent domainEvent)
    : INotification where TDomainEvent : IDomainEvent
{
    public TDomainEvent DomainEvent { get; } = domainEvent;
}
