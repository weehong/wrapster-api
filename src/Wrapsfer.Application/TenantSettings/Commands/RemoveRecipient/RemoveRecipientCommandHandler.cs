using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.TenantSettings.Commands.RemoveRecipient;

internal sealed class RemoveRecipientCommandHandler(
    ITenantSettingsRepository tenantSettingsRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<RemoveRecipientCommand>
{
    public async Task<Result> Handle(RemoveRecipientCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        TenantSettingsEntity? settings =
            await tenantSettingsRepository.GetByTenantIdAsync(tenantId, cancellationToken);

        if (settings is null)
        {
            return Result.Failure(TenantSettingsErrors.NotFound);
        }

        Result removeResult = settings.RemoveRecipient(request.RecipientId);
        if (removeResult.IsFailure)
        {
            return removeResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
