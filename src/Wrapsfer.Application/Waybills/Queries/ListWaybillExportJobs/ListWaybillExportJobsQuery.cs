using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.Waybills.Responses;

namespace Wrapsfer.Application.Waybills.Queries.ListWaybillExportJobs;

public sealed record ListWaybillExportJobsQuery(
    int Page = 1,
    int PageSize = 50) : IQuery<PagedResult<WaybillExportJobResponse>>;
