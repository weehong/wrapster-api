using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Waybills.Responses;

namespace Wrapster.Application.Waybills.Queries.GetWaybillsByDate;

public sealed record GetWaybillsByDateQuery(DateOnly PackagingDate) : IQuery<IReadOnlyList<WaybillResponse>>;
