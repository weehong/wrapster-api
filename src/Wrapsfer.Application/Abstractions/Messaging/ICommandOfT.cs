using MediatR;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Abstractions.Messaging;

public interface ICommand<TResponse> : IRequest<Result<TResponse>>;
