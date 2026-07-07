using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Waybills.Commands.UpdateWaybillNumber;

public sealed record UpdateWaybillNumberCommand(Guid Id, string WaybillNumber) : ICommand;
