using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.TenantSettings.Commands.SetRecipientActive;

internal sealed class SetRecipientActiveCommandHandler(
    ITenantSettingsRepository tenantSettingsRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<SetRecipientActiveCommand>
{
    public async Task<Result> Handle(SetRecipientActiveCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        TenantSettingsEntity? settings =
            await tenantSettingsRepository.GetByTenantIdAsync(tenantId, cancellationToken);

        if (settings is null)
        {
            return Result.Failure(TenantSettingsErrors.NotFound);
        }

        Result result = settings.SetRecipientActive(request.RecipientId, request.IsActive);
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
