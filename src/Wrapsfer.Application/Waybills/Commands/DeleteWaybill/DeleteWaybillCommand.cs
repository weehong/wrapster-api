using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Waybills.Commands.DeleteWaybill;

public sealed record DeleteWaybillCommand(Guid Id) : ICommand;
