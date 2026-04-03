using MediatR;
using Wrapster.Domain.Common;

namespace Wrapster.Application.Abstractions.Messaging;

public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result> where TCommand : ICommand;
