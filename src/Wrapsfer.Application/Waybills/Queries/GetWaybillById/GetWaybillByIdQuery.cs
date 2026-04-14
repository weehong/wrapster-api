using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Responses;

namespace Wrapsfer.Application.Waybills.Queries.GetWaybillById;

public sealed record GetWaybillByIdQuery(Guid Id) : IQuery<WaybillResponse>;
