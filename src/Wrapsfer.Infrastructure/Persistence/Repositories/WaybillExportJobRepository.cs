using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class WaybillExportJobRepository(ApplicationDbContext context) : IWaybillExportJobRepository
{
    public async Task<WaybillExportJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.WaybillExportJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<WaybillExportJob> Items, int TotalCount)> ListByUserAsync(
        string requestedByUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<WaybillExportJob> query = context.WaybillExportJobs
            .Where(j => j.RequestedByUserId == requestedByUserId)
            .OrderByDescending(j => j.CreatedAt);

        int totalCount = await query.CountAsync(cancellationToken);

        List<WaybillExportJob> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Add(WaybillExportJob job) => context.WaybillExportJobs.Add(job);

    public async Task MarkProcessingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        WaybillExportJob? job = await GetByIdAsync(id, cancellationToken);
        if (job is null)
        {
            return;
        }

        job.MarkProcessing();
    }

    public async Task MarkCompletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        WaybillExportJob? job = await GetByIdAsync(id, cancellationToken);
        if (job is null)
        {
            return;
        }

        job.MarkCompleted();
    }

    public async Task MarkFailedAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        WaybillExportJob? job = await GetByIdAsync(id, cancellationToken);
        if (job is null)
        {
            return;
        }

        job.MarkFailed(reason);
    }
}
