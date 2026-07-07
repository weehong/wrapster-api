using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.TenantSettings.Commands.AddRecipient;

internal sealed class AddRecipientCommandHandler(
    ITenantSettingsRepository tenantSettingsRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<AddRecipientCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AddRecipientCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        TenantSettingsEntity? settings =
            await tenantSettingsRepository.GetByTenantIdAsync(tenantId, cancellationToken);

        if (settings is null)
        {
            Result<TenantSettingsEntity> createResult = TenantSettingsEntity.Create(tenantId);
            if (createResult.IsFailure)
            {
                return Result<Guid>.Failure(createResult.Error);
            }

            settings = createResult.Value;
            tenantSettingsRepository.Add(settings);
        }

        Result<NotificationRecipient> addResult = settings.AddRecipient(request.Email);
        if (addResult.IsFailure)
        {
            return Result<Guid>.Failure(addResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return addResult.Value.Id;
    }
}
