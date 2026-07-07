using MediatR;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Abstractions.Messaging;

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;
