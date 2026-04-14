using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.Waybills.Responses;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Waybills.Queries.ListWaybills;

public sealed record ListWaybillsQuery(
    DateOnly? From,
    DateOnly? To,
    WaybillStatus? Status,
    string? Search,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<WaybillResponse>>;
