using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.Waybills.Commands.UpdateWaybillItemQuantity;

public sealed record UpdateWaybillItemQuantityCommand(
    Guid WaybillId,
    Guid ItemId,
    int Quantity) : ICommand;
