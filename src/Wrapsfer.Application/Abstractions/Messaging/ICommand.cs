using MediatR;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Abstractions.Messaging;

public interface ICommand : IRequest<Result>;
