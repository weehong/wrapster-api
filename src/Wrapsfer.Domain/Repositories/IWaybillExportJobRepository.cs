using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Domain.Repositories;

public interface IWaybillExportJobRepository
{
    Task<WaybillExportJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<WaybillExportJob> Items, int TotalCount)> ListByUserAsync(
        string requestedByUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    void Add(WaybillExportJob job);

    Task MarkProcessingAsync(Guid id, CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(Guid id, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(Guid id, string reason, CancellationToken cancellationToken = default);
}
