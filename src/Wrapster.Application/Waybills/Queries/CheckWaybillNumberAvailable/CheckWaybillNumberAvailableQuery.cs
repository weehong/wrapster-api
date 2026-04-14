using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.Waybills.Queries.CheckWaybillNumberAvailable;

public sealed record CheckWaybillNumberAvailableQuery(string WaybillNumber) : IQuery<bool>;
