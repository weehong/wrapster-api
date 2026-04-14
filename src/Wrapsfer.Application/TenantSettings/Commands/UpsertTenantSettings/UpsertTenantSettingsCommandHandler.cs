using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Repositories;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.TenantSettings.Commands.UpsertTenantSettings;

internal sealed class UpsertTenantSettingsCommandHandler(
    ITenantSettingsRepository tenantSettingsRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<UpsertTenantSettingsCommand>
{
    public async Task<Result> Handle(UpsertTenantSettingsCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        TenantSettingsEntity? settings =
            await tenantSettingsRepository.GetByTenantIdAsync(tenantId, cancellationToken);

        int? threshold = request.ClearDefaultLowStockThreshold
            ? null
            : request.DefaultLowStockThreshold;

        if (settings is null)
        {
            Result<TenantSettingsEntity> createResult = TenantSettingsEntity.Create(tenantId, threshold);
            if (createResult.IsFailure)
            {
                return Result.Failure(createResult.Error);
            }

            tenantSettingsRepository.Add(createResult.Value);
        }
        else if (request.ClearDefaultLowStockThreshold || request.DefaultLowStockThreshold.HasValue)
        {
            Result updateResult = settings.SetDefaultLowStockThreshold(threshold);
            if (updateResult.IsFailure)
            {
                return updateResult;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
