using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Waybills.Commands.CancelWaybill;

public sealed record CancelWaybillCommand(Guid Id, string Reason) : ICommand;
