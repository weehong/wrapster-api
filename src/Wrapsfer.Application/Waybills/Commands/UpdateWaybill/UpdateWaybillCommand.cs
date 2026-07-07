using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Waybills.Commands.UpdateWaybill;

public sealed record UpdateWaybillCommand(
    Guid Id,
    DateOnly PackagingDate,
    string WaybillNumber,
    IReadOnlyList<UpdateWaybillItem> Items) : ICommand;
