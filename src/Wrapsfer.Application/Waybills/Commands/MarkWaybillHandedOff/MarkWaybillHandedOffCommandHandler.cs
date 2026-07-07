using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.Products;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.Waybills.Commands.MarkWaybillHandedOff;

internal sealed class MarkWaybillHandedOffCommandHandler(
    IWaybillRepository waybillRepository,
    ITenantSettingsRepository tenantSettingsRepository,
    StockReservationService stockReservationService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IOptions<ProductSettings> productSettings) : ICommandHandler<MarkWaybillHandedOffCommand>
{
    public async Task<Result> Handle(MarkWaybillHandedOffCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        Waybill? waybill =
            await waybillRepository.GetByIdWithItemsAsync(request.Id, tenantId, cancellationToken);
        if (waybill is null)
        {
            return Result.Failure(WaybillErrors.NotFound);
        }

        bool isAdmin = tenantContext.Roles.Contains(TenantRoles.Admin, StringComparer.Ordinal);

        Result markResult = waybill.MarkHandedOff(tenantContext.UserId, isAdmin);
        if (markResult.IsFailure)
        {
            return markResult;
        }

        TenantSettingsEntity? settings = await tenantSettingsRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        int fallbackThreshold = settings?.DefaultLowStockThreshold ?? productSettings.Value.GlobalLowStockThreshold;

        Result consumeResult =
            await stockReservationService.ConsumeItemsAsync(waybill.Items, tenantId, fallbackThreshold, cancellationToken);
        if (consumeResult.IsFailure)
        {
            return consumeResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
