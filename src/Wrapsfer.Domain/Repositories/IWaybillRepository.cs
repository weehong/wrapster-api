using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Domain.Repositories;

public interface IWaybillRepository
{
    Task<Waybill?> GetByIdAsync(Guid id, string tenantId, CancellationToken cancellationToken = default);

    Task<Waybill?> GetByIdWithItemsAsync(Guid id, string tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Waybill>> GetByIdsWithItemsAsync(IEnumerable<Guid> ids, string tenantId,
        CancellationToken cancellationToken = default);

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

    Task<(IReadOnlyList<Waybill> Items, int TotalCount)> ListByTenantIdsAsync(
        IReadOnlyCollection<string> tenantIds,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        WaybillStatus? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Waybill>> GetStaleDraftsAsync(DateOnly olderThan,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Waybill>> GetAutoCancelledBetweenAsync(string tenantId, DateTime? fromUtc,
        DateTime? toExclusiveUtc, CancellationToken cancellationToken = default);

    void Add(Waybill waybill);
    void Remove(Waybill waybill);

    /// <summary>
    /// Stops tracking the waybill so pending in-memory changes are not persisted by a later save.
    /// Used to discard a failed transition during partial-success batch processing.
    /// </summary>
    void Detach(Waybill waybill);
}
