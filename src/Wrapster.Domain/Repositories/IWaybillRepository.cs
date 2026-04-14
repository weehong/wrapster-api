using Wrapster.Domain.Entities;
using Wrapster.Domain.Enums;

namespace Wrapster.Domain.Repositories;

public interface IWaybillRepository
{
    Task<Waybill?> GetByIdAsync(Guid id, string tenantId, CancellationToken cancellationToken = default);

    Task<Waybill?> GetByIdWithItemsAsync(Guid id, string tenantId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNumberAsync(string waybillNumber, string tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByNumberAsync(string waybillNumber, Guid excludeId, string tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Waybill>> GetByDateAsync(DateOnly packagingDate, string tenantId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Waybill> Items, int TotalCount)> ListAsync(
        string tenantId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        WaybillStatus? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Waybill>> GetStaleDraftsAsync(DateOnly olderThan,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Waybill>> GetRecentlyAutoCancelledAsync(string tenantId, DateTime since,
        CancellationToken cancellationToken = default);

    void Add(Waybill waybill);
    void Remove(Waybill waybill);
}
