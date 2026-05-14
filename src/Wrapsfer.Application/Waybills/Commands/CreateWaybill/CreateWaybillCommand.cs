using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Waybills.Commands.CreateWaybill;

public sealed record CreateWaybillCommand(
    DateOnly PackagingDate,
    string WaybillNumber,
    IReadOnlyList<CreateWaybillItem> Items) : ICommand<Guid>;
