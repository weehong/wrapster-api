using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Repositories;
using TenantSettingsEntity = Wrapster.Domain.Entities.TenantSettings;

namespace Wrapster.Application.TenantSettings.Commands.AddRecipient;

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
