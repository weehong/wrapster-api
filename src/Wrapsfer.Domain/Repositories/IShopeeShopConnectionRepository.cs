using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Domain.Repositories;

public interface IShopeeShopConnectionRepository
{
    Task<ShopeeShopConnection?> GetByTenantIdAsync(
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsForTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists connections whose access token expires before <paramref name="accessTokenExpiresBefore"/>
    /// and whose refresh token is still usable after <paramref name="refreshTokenExpiresAfter"/>.
    /// Intentionally spans all tenants: it exists solely for the system-level background token
    /// refresh job, never for user-driven request handling.
    /// </summary>
    Task<IReadOnlyList<ShopeeShopConnection>> ListRequiringRefreshAsync(
        DateTime accessTokenExpiresBefore,
        DateTime refreshTokenExpiresAfter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the connection owning a Shopee shop ID. Spans all tenants by design —
    /// used only to route incoming Shopee push messages to a tenant.
    /// </summary>
    Task<ShopeeShopConnection?> GetByShopIdAsync(long shopId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every connection. Spans all tenants by design — used only by the
    /// order reconciliation background job.
    /// </summary>
    Task<IReadOnlyList<ShopeeShopConnection>> ListAllAsync(CancellationToken cancellationToken = default);

    void Add(ShopeeShopConnection connection);

    void Remove(ShopeeShopConnection connection);
}
