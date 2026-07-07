using MediatR;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Abstractions.Messaging;

public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result> where TCommand : ICommand;
