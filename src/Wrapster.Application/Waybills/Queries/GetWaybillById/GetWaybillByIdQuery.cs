using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Waybills.Responses;

namespace Wrapster.Application.Waybills.Queries.GetWaybillById;

public sealed record GetWaybillByIdQuery(Guid Id) : IQuery<WaybillResponse>;
