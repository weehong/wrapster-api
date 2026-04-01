using MediatR;
using Wrapster.Domain.Common;

namespace Wrapster.Application.Abstractions.Messaging;

public interface ICommand : IRequest<Result>;

public interface ICommand<TResponse> : IRequest<Result<TResponse>>;
