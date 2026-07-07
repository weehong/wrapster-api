using MediatR;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Abstractions;

public sealed class DomainEventNotification<TDomainEvent>(TDomainEvent domainEvent)
    : INotification where TDomainEvent : IDomainEvent
{
    public TDomainEvent DomainEvent { get; } = domainEvent;
}
