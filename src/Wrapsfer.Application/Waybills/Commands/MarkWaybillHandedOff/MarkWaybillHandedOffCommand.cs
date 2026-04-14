using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Waybills.Commands.MarkWaybillHandedOff;

public sealed record MarkWaybillHandedOffCommand(Guid Id) : ICommand;
