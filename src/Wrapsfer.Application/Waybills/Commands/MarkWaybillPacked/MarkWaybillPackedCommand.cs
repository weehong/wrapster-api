using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Waybills.Commands.MarkWaybillPacked;

public sealed record MarkWaybillPackedCommand(Guid Id) : ICommand;
