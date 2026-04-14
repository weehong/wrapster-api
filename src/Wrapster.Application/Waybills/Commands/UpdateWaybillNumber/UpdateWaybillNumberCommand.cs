using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.Waybills.Commands.UpdateWaybillNumber;

public sealed record UpdateWaybillNumberCommand(Guid Id, string WaybillNumber) : ICommand;
