using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Common;
using Wrapster.Application.Waybills.Responses;
using Wrapster.Domain.Enums;

namespace Wrapster.Application.Waybills.Queries.ListWaybills;

public sealed record ListWaybillsQuery(
    DateOnly? From,
    DateOnly? To,
    WaybillStatus? Status,
    string? Search,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<WaybillResponse>>;
