using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.Waybills.Commands.CancelWaybill;

public sealed record CancelWaybillCommand(Guid Id, string Reason) : ICommand;
