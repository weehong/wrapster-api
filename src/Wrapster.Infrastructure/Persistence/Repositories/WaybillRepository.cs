using Microsoft.EntityFrameworkCore;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Enums;
using Wrapster.Domain.Repositories;

namespace Wrapster.Infrastructure.Persistence.Repositories;

internal sealed class WaybillRepository(ApplicationDbContext context) : IWaybillRepository
{
    public async Task<Waybill?> GetByIdAsync(Guid id, string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.Waybills
            .FirstOrDefaultAsync(w => w.Id == id && w.TenantId == tenantId, cancellationToken);

    public async Task<Waybill?> GetByIdWithItemsAsync(Guid id, string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.Waybills
            .Include(w => w.Items)
            .FirstOrDefaultAsync(w => w.Id == id && w.TenantId == tenantId, cancellationToken);

    public async Task<bool> ExistsByNumberAsync(string waybillNumber, string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.Waybills
            .AnyAsync(w => w.TenantId == tenantId && w.WaybillNumber == waybillNumber, cancellationToken);

    public async Task<bool> ExistsByNumberAsync(string waybillNumber, Guid excludeId, string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.Waybills
            .AnyAsync(w => w.TenantId == tenantId && w.WaybillNumber == waybillNumber && w.Id != excludeId,
                cancellationToken);

    public async Task<IReadOnlyList<Waybill>> GetByDateAsync(DateOnly packagingDate, string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.Waybills
            .Include(w => w.Items)
            .Where(w => w.TenantId == tenantId && w.PackagingDate == packagingDate)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Waybill> Items, int TotalCount)> ListAsync(
        string tenantId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        WaybillStatus? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Waybill> query = context.Waybills
            .Include(w => w.Items)
            .Where(w => w.TenantId == tenantId);

        if (fromDate.HasValue)
        {
            query = query.Where(w => w.PackagingDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(w => w.PackagingDate <= toDate.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(w => w.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search}%";
            query = query.Where(w => EF.Functions.ILike(w.WaybillNumber, pattern));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        List<Waybill> items = await query
            .OrderByDescending(w => w.PackagingDate)
            .ThenByDescending(w => w.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Waybill>> GetStaleDraftsAsync(DateOnly olderThan,
        CancellationToken cancellationToken = default) =>
        await context.Waybills
            .Include(w => w.Items)
            .Where(w => w.Status == WaybillStatus.Draft && w.PackagingDate < olderThan)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Waybill>> GetRecentlyAutoCancelledAsync(string tenantId, DateTime since,
        CancellationToken cancellationToken = default) =>
        await context.Waybills
            .Include(w => w.Items)
            .Where(w => w.TenantId == tenantId
                        && w.Status == WaybillStatus.Cancelled
                        && w.CancelledAt >= since
                        && w.CancellationReason != null
                        && w.CancellationReason.StartsWith("Auto-cancelled"))
            .OrderByDescending(w => w.CancelledAt)
            .ToListAsync(cancellationToken);

    public void Add(Waybill waybill) => context.Waybills.Add(waybill);

    public void Remove(Waybill waybill) => context.Waybills.Remove(waybill);
}
