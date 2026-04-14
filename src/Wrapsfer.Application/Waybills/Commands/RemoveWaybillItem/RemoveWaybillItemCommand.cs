using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Waybills.Commands.RemoveWaybillItem;

public sealed record RemoveWaybillItemCommand(Guid WaybillId, Guid ItemId) : ICommand;
