using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Waybills.Commands.AddWaybillItem;

public sealed record AddWaybillItemCommand(
    Guid WaybillId,
    string Barcode,
    int Quantity = 1) : ICommand<Guid>;
