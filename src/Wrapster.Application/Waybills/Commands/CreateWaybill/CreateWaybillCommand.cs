using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.Waybills.Commands.CreateWaybill;

public sealed record CreateWaybillCommand(
    DateOnly PackagingDate,
    string WaybillNumber) : ICommand<Guid>;
