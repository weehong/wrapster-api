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

    void Add(ShopeeShopConnection connection);

    void Remove(ShopeeShopConnection connection);
}
