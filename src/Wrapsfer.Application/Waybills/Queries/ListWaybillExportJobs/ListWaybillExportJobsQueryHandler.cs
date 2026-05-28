using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.Waybills.Common;
using Wrapsfer.Application.Waybills.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Queries.ListWaybillExportJobs;

internal sealed class ListWaybillExportJobsQueryHandler(
    IWaybillExportJobRepository jobRepository,
    ITenantContext tenantContext) : IQueryHandler<ListWaybillExportJobsQuery, PagedResult<WaybillExportJobResponse>>
{
    public async Task<Result<PagedResult<WaybillExportJobResponse>>> Handle(
        ListWaybillExportJobsQuery request,
        CancellationToken cancellationToken)
    {
        (IReadOnlyList<WaybillExportJob> items, int totalCount) = await jobRepository.ListByUserAsync(
            tenantContext.UserId,
            request.Page,
            request.PageSize,
            cancellationToken);

        List<WaybillExportJobResponse> responses = items
            .Select(job => new WaybillExportJobResponse(
                job.Id,
                job.Format,
                job.Status,
                WaybillExportJobSerializer.DeserializeIds(job.PartnerTenantIdsJson),
                job.FromDate,
                job.ToDate,
                WaybillExportJobSerializer.DeserializeEmails(job.RecipientEmailsJson),
                job.CreatedAt,
                job.CompletedAt,
                job.FailureReason))
            .ToList();

        return new PagedResult<WaybillExportJobResponse>(
            responses,
            totalCount,
            request.Page,
            request.PageSize);
    }
}
