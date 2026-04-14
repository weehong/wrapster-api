using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.Waybills.Commands.MarkWaybillHandedOff;

public sealed record MarkWaybillHandedOffCommand(Guid Id) : ICommand;
