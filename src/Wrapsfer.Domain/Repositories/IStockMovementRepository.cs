namespace Wrapsfer.Domain.Repositories;

public interface IStockMovementRepository
{
    /// <summary>
    /// Returns each stock-holding product's quantity and unit cost as of <paramref name="asOfUtcExclusive"/>,
    /// taken from its latest movement strictly before that instant. Bundles are excluded (they hold no stock).
    /// </summary>
    Task<IReadOnlyList<ProductStockSnapshot>> GetPointInTimeSnapshotAsync(
        string tenantId,
        DateTime asOfUtcExclusive,
        CancellationToken cancellationToken = default);
}
