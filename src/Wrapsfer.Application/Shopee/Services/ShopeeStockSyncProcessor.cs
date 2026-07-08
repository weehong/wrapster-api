using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Services;

public sealed class ShopeeStockSyncProcessor(
    IShopeeProductLinkRepository linkRepository,
    IShopeeShopConnectionRepository connectionRepository,
    IProductRepository productRepository,
    IShopeeGateway shopeeGateway,
    IUnitOfWork unitOfWork,
    ILogger<ShopeeStockSyncProcessor> logger)
{
    private static readonly TimeSpan s_tokenRefreshWindow = TimeSpan.FromMinutes(5);

    public async Task<ShopeeStockSyncRunSummary> RunAsync(CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        IReadOnlyList<ShopeeProductLink> links =
            await linkRepository.ListPendingSyncAsync(now, 200, cancellationToken);

        int synced = 0;
        int failed = 0;
        int skipped = 0;

        foreach (IGrouping<string, ShopeeProductLink> group in links.GroupBy(l => l.TenantId))
        {
            ShopeeShopConnection? connection =
                await connectionRepository.GetByTenantIdAsync(group.Key, cancellationToken);
            if (connection is null)
            {
                foreach (ShopeeProductLink link in group)
                {
                    link.MarkSyncFailed(ShopeeProductLinkErrors.ConnectionNotFound.Code, DateTime.UtcNow);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    failed++;
                }

                continue;
            }

            IReadOnlyList<Product> products = await productRepository.GetByIdsAsync(
                group.Select(l => l.ProductId), group.Key, cancellationToken);
            Dictionary<Guid, Product> productsById = products.ToDictionary(p => p.Id);

            await RefreshIfNeededAsync(connection, cancellationToken);

            foreach (ShopeeProductLink link in group)
            {
                if (!productsById.TryGetValue(link.ProductId, out Product? product))
                {
                    skipped++;
                    continue;
                }

                bool success = await PushLinkAsync(connection, link, product, DateTime.UtcNow, true, cancellationToken);
                if (success)
                {
                    synced++;
                }
                else
                {
                    failed++;
                }
            }
        }

        return new ShopeeStockSyncRunSummary(synced, failed, skipped);
    }

    public async Task<Result<ShopeeStockSyncRunSummary>> RunForTenantAsync(
        string tenantId,
        Guid? linkId,
        CancellationToken cancellationToken)
    {
        ShopeeShopConnection? connection =
            await connectionRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        if (connection is null)
        {
            return Result<ShopeeStockSyncRunSummary>.Failure(ShopeeProductLinkErrors.ConnectionNotFound);
        }

        IReadOnlyList<ShopeeProductLink> links;
        if (linkId.HasValue)
        {
            ShopeeProductLink? link =
                await linkRepository.GetByIdAsync(linkId.Value, tenantId, cancellationToken);
            if (link is null)
            {
                return Result<ShopeeStockSyncRunSummary>.Failure(ShopeeProductLinkErrors.NotFound);
            }

            links = [link];
        }
        else
        {
            links = await linkRepository.ListByTenantAsync(tenantId, cancellationToken);
        }

        IReadOnlyList<Product> products = await productRepository.GetByIdsAsync(
            links.Select(l => l.ProductId), tenantId, cancellationToken);
        Dictionary<Guid, Product> productsById = products.ToDictionary(p => p.Id);

        await RefreshIfNeededAsync(connection, cancellationToken);

        int synced = 0;
        int failed = 0;
        int skipped = 0;

        foreach (ShopeeProductLink link in links)
        {
            if (!productsById.TryGetValue(link.ProductId, out Product? product))
            {
                skipped++;
                continue;
            }

            bool success = await PushLinkAsync(connection, link, product, DateTime.UtcNow, true, cancellationToken);
            if (success)
            {
                synced++;
            }
            else
            {
                failed++;
            }
        }

        return Result<ShopeeStockSyncRunSummary>.Success(new ShopeeStockSyncRunSummary(synced, failed, skipped));
    }

    private async Task RefreshIfNeededAsync(
        ShopeeShopConnection connection,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        if (connection.AccessTokenExpiresAt > now.Add(s_tokenRefreshWindow))
        {
            return;
        }

        bool refreshed = await RefreshConnectionAsync(connection, cancellationToken);
        if (refreshed)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<bool> PushLinkAsync(
        ShopeeShopConnection connection,
        ShopeeProductLink link,
        Product product,
        DateTime attemptedAt,
        bool retryAuthFailure,
        CancellationToken cancellationToken)
    {
        int targetQuantity = product.IsActive ? Math.Max(product.AvailableQuantity, 0) : 0;
        Result updateResult = await shopeeGateway.UpdateStockAsync(
            connection.ShopId,
            connection.AccessToken,
            link.ShopeeItemId,
            link.ShopeeModelId,
            targetQuantity,
            cancellationToken);

        if (updateResult.IsSuccess)
        {
            link.MarkSynced(targetQuantity, attemptedAt);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (retryAuthFailure && updateResult.Error.Code == ShopeeProductLinkErrors.AuthFailed.Code)
        {
            bool refreshed = await RefreshConnectionAsync(connection, cancellationToken);
            if (refreshed)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return await PushLinkAsync(connection, link, product, DateTime.UtcNow, false, cancellationToken);
            }
        }

        link.MarkSyncFailed(updateResult.Error.Code, attemptedAt);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return false;
    }

    private async Task<bool> RefreshConnectionAsync(
        ShopeeShopConnection connection,
        CancellationToken cancellationToken)
    {
        Result<ShopeeTokenGrant> grantResult = await shopeeGateway.RefreshAccessTokenAsync(
            connection.RefreshToken, connection.ShopId, cancellationToken);
        if (grantResult.IsFailure)
        {
            logger.LogWarning(
                "Shopee token refresh before stock sync failed for tenant {TenantId}, shop {ShopId}: {ErrorCode}",
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
                "Shopee token refresh before stock sync returned invalid tokens for tenant {TenantId}, shop {ShopId}: {ErrorCode}",
                connection.TenantId, connection.ShopId, updateResult.Error.Code);
            return false;
        }

        return true;
    }
}
