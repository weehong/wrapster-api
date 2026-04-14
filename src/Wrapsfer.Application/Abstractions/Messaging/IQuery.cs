using MediatR;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Abstractions.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
