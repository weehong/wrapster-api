using MediatR;
using Wrapster.Domain.Common;

namespace Wrapster.Application.Abstractions.Messaging;

public interface ICommand : IRequest<Result>;
