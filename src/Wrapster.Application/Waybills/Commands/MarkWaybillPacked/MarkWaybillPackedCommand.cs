using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.Waybills.Commands.MarkWaybillPacked;

public sealed record MarkWaybillPackedCommand(Guid Id) : ICommand;
