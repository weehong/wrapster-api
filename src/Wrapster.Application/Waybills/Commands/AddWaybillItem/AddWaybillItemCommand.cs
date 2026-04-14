using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.Waybills.Commands.AddWaybillItem;

public sealed record AddWaybillItemCommand(
    Guid WaybillId,
    string Barcode,
    int Quantity = 1) : ICommand<Guid>;
