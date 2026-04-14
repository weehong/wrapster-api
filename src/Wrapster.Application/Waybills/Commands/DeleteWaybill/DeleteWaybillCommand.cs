using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.Waybills.Commands.DeleteWaybill;

public sealed record DeleteWaybillCommand(Guid Id) : ICommand;
