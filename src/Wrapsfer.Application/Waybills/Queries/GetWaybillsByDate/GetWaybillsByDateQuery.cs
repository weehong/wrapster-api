using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Responses;

namespace Wrapsfer.Application.Waybills.Queries.GetWaybillsByDate;

public sealed record GetWaybillsByDateQuery(DateOnly PackagingDate) : IQuery<IReadOnlyList<WaybillResponse>>;
