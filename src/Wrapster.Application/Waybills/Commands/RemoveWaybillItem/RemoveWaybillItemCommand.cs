using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.Waybills.Commands.RemoveWaybillItem;

public sealed record RemoveWaybillItemCommand(Guid WaybillId, Guid ItemId) : ICommand;
