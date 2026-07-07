using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Waybills.Commands.UpdateWaybillItemQuantity;

public sealed record UpdateWaybillItemQuantityCommand(
    Guid WaybillId,
    Guid ItemId,
    int Quantity) : ICommand;
