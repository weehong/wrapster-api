using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Waybills.Queries.CheckWaybillNumberAvailable;

public sealed record CheckWaybillNumberAvailableQuery(string WaybillNumber) : IQuery<bool>;
