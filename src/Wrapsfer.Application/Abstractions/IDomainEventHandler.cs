using MediatR;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Abstractions;

public interface IDomainEventHandler<TDomainEvent>
    : INotificationHandler<DomainEventNotification<TDomainEvent>> where TDomainEvent : IDomainEvent;
