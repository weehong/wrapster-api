using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Domain.Repositories;

public interface IWaybillRepository
{
    Task<Waybill?> GetByIdAsync(Guid id, string tenantId, CancellationToken cancellationToken = default);

    Task<Waybill?> GetByIdWithItemsAsync(Guid id, string tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Waybill>> GetByIdsWithItemsAsync(IEnumerable<Guid> ids, string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deliberately NOT tenant-scoped — the single sanctioned exception to the repository
    /// tenant-isolation rule. Waybill numbers are carrier tracking numbers and must be unique
    /// across ALL tenants (enforced by the unique index on WaybillNumber); a per-tenant check
    /// cannot detect a number already registered under another tenant.
    /// </summary>
    Task<bool> ExistsByNumberInAnyTenantAsync(string waybillNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cross-tenant existence check excluding one waybill (by globally unique primary key),
    /// for update paths. See <see cref="ExistsByNumberInAnyTenantAsync(string, CancellationToken)"/>.
    /// </summary>
    Task<bool> ExistsByNumberInAnyTenantAsync(string waybillNumber, Guid excludeId,
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
