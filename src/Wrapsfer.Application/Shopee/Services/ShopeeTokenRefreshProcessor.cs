using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Services;

public sealed class ShopeeTokenRefreshProcessor(
    IShopeeShopConnectionRepository connectionRepository,
    IShopeeGateway shopeeGateway,
    IUnitOfWork unitOfWork,
    ILogger<ShopeeTokenRefreshProcessor> logger)
{
    /// <summary>
    /// Access tokens are refreshed this long before they expire so shop API calls
    /// never race the ~4-hour expiry. Refreshing also rotates the refresh token,
    /// keeping the 30-day re-authorization deadline rolling.
    /// </summary>
    public static readonly TimeSpan RefreshWindow = TimeSpan.FromHours(1);

    public async Task<ShopeeTokenRefreshRunSummary> RunAsync(CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        IReadOnlyList<ShopeeShopConnection> connections =
            await connectionRepository.ListRequiringRefreshAsync(
                now.Add(RefreshWindow), now, cancellationToken);

        int refreshed = 0;
        int failed = 0;
        int expired = 0;

        foreach (ShopeeShopConnection connection in connections)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // The repository already excludes refresh-expired connections; this re-check only
            // covers tokens that lapsed between the query and this iteration.
            if (connection.RefreshTokenExpiresAt <= DateTime.UtcNow)
            {
                expired++;
                logger.LogWarning(
                    "Shopee refresh token expired for tenant {TenantId}, shop {ShopId}; the partner must re-link the shop",
                    connection.TenantId, connection.ShopId);
                continue;
            }

            bool success = await RefreshConnectionAsync(connection, cancellationToken);
            if (success)
            {
                refreshed++;
            }
            else
            {
                failed++;
            }
        }

        return new ShopeeTokenRefreshRunSummary(refreshed, failed, expired);
    }

    private async Task<bool> RefreshConnectionAsync(
        ShopeeShopConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            Result<ShopeeTokenGrant> grantResult = await shopeeGateway.RefreshAccessTokenAsync(
                connection.RefreshToken, connection.ShopId, cancellationToken);
            if (grantResult.IsFailure)
            {
                logger.LogWarning(
                    "Shopee token refresh failed for tenant {TenantId}, shop {ShopId}: {ErrorCode}",
                    connection.TenantId, connection.ShopId, grantResult.Error.Code);
                return false;
            }

            ShopeeTokenGrant grant = grantResult.Value;
            DateTime now = DateTime.UtcNow;

            Result updateResult = connection.UpdateTokens(
                grant.AccessToken,
                grant.RefreshToken,
                now.AddSeconds(grant.ExpiresInSeconds),
                now.Add(ShopeeShopConnection.RefreshTokenLifetime));
            if (updateResult.IsFailure)
            {
                logger.LogWarning(
                    "Shopee token refresh produced an invalid grant for tenant {TenantId}, shop {ShopId}: {ErrorCode}",
                    connection.TenantId, connection.ShopId, updateResult.Error.Code);
                return false;
            }

            // Save per connection so one failure does not roll back refreshes that
            // already consumed their single-use refresh token.
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Shopee token refresh threw for tenant {TenantId}, shop {ShopId}",
                connection.TenantId, connection.ShopId);
            return false;
        }
    }
}
