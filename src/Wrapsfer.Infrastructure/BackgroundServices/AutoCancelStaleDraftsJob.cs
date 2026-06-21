using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.BackgroundServices;

public sealed class AutoCancelStaleDraftsJob(
    IServiceScopeFactory scopeFactory,
    ILogger<AutoCancelStaleDraftsJob> logger) : BackgroundService
{
    private static readonly TimeSpan s_interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(s_interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ExecuteOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }

    private async Task ExecuteOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<(Guid Id, string TenantId)> staleIds;

        try
        {
            using IServiceScope discoveryScope = scopeFactory.CreateScope();
            IWaybillRepository discoveryRepository =
                discoveryScope.ServiceProvider.GetRequiredService<IWaybillRepository>();

            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
            IReadOnlyList<Waybill> staleDrafts =
                await discoveryRepository.GetStaleDraftsAsync(today, cancellationToken);

            staleIds = staleDrafts.Select(w => (w.Id, w.TenantId)).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AutoCancelStaleDraftsJob discovery phase failed");
            return;
        }

        if (staleIds.Count == 0)
        {
            return;
        }

        int cancelledCount = 0;
        foreach ((Guid waybillId, string tenantId) in staleIds)
        {
            if (await TryCancelWaybillAsync(waybillId, tenantId, cancellationToken))
            {
                cancelledCount++;
            }
        }

        logger.LogInformation(
            "Auto-cancelled {Count} of {Total} stale draft waybills",
            cancelledCount, staleIds.Count);
    }

    private async Task<bool> TryCancelWaybillAsync(Guid waybillId, string tenantId,
        CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IWaybillRepository waybillRepository =
                scope.ServiceProvider.GetRequiredService<IWaybillRepository>();
            StockReservationService stockReservationService =
                scope.ServiceProvider.GetRequiredService<StockReservationService>();
            IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            Waybill? waybill =
                await waybillRepository.GetByIdWithItemsAsync(waybillId, tenantId, cancellationToken);
            if (waybill is null)
            {
                return false;
            }

            if (waybill.Items.Count > 0)
            {
                Result releaseResult = await stockReservationService.ReleaseItemsAsync(
                    waybill.Items, tenantId, cancellationToken);
                if (releaseResult.IsFailure)
                {
                    logger.LogError(
                        "Skipping auto-cancel for waybill {WaybillId} (tenant {TenantId}): reservation release failed — {Error}",
                        waybillId, tenantId, releaseResult.Error.Description);
                    return false;
                }
            }

            Result cancelResult = waybill.Cancel(Waybill.AutoCancelledStaleDraftReason);
            if (cancelResult.IsFailure)
            {
                logger.LogWarning(
                    "Failed to auto-cancel waybill {WaybillId} for tenant {TenantId}: {Error}",
                    waybillId, tenantId, cancelResult.Error.Description);
                return false;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to auto-cancel waybill {WaybillId} for tenant {TenantId}",
                waybillId, tenantId);
            return false;
        }
    }
}
