using MediatR;
using Wrapster.Domain.Common;

namespace Wrapster.Application.Abstractions;

public interface IDomainEventHandler<TDomainEvent>
    : INotificationHandler<DomainEventNotification<TDomainEvent>> where TDomainEvent : IDomainEvent;
