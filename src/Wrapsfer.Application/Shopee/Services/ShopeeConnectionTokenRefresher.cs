using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;

namespace Wrapsfer.Application.Shopee.Services;

/// <summary>
/// Refreshes a connection's Shopee tokens when they are about to expire. Shared by the
/// order sync/webhook/reconciliation paths (the stock sync processor keeps its own copy).
/// </summary>
public sealed class ShopeeConnectionTokenRefresher(
    IShopeeGateway shopeeGateway,
    IUnitOfWork unitOfWork,
    ILogger<ShopeeConnectionTokenRefresher> logger)
{
    private static readonly TimeSpan s_refreshWindow = TimeSpan.FromMinutes(5);

    public async Task RefreshIfNeededAsync(ShopeeShopConnection connection, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        if (connection.AccessTokenExpiresAt > now.Add(s_refreshWindow))
        {
            return;
        }

        Result<ShopeeTokenGrant> grantResult = await shopeeGateway.RefreshAccessTokenAsync(
            connection.RefreshToken, connection.ShopId, cancellationToken);
        if (grantResult.IsFailure)
        {
            logger.LogWarning(
                "Shopee token refresh before order sync failed for tenant {TenantId}, shop {ShopId}: {ErrorCode}",
                connection.TenantId, connection.ShopId, grantResult.Error.Code);
            return;
        }

        ShopeeTokenGrant grant = grantResult.Value;
        Result updateResult = connection.UpdateTokens(
            grant.AccessToken,
            grant.RefreshToken,
            now.AddSeconds(grant.ExpiresInSeconds),
            now.Add(ShopeeShopConnection.RefreshTokenLifetime));
        if (updateResult.IsFailure)
        {
            logger.LogWarning(
                "Shopee token refresh before order sync returned invalid tokens for tenant {TenantId}, shop {ShopId}: {ErrorCode}",
                connection.TenantId, connection.ShopId, updateResult.Error.Code);
            return;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
