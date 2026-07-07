using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class ShopeeShopConnectionRepository(ApplicationDbContext context)
    : IShopeeShopConnectionRepository
{
    public async Task<ShopeeShopConnection?> GetByTenantIdAsync(
        string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.ShopeeShopConnections
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);

    public async Task<bool> ExistsForTenantAsync(string tenantId, CancellationToken cancellationToken = default) =>
        await context.ShopeeShopConnections
            .AnyAsync(c => c.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<ShopeeShopConnection>> ListRequiringRefreshAsync(
        DateTime accessTokenExpiresBefore,
        DateTime refreshTokenExpiresAfter,
        CancellationToken cancellationToken = default) =>
        await context.ShopeeShopConnections
            .Where(c => c.AccessTokenExpiresAt < accessTokenExpiresBefore
                        && c.RefreshTokenExpiresAt > refreshTokenExpiresAfter)
            .OrderBy(c => c.AccessTokenExpiresAt)
            .ToListAsync(cancellationToken);

    public void Add(ShopeeShopConnection connection) =>
        context.ShopeeShopConnections.Add(connection);

    public void Remove(ShopeeShopConnection connection) =>
        context.ShopeeShopConnections.Remove(connection);
}
