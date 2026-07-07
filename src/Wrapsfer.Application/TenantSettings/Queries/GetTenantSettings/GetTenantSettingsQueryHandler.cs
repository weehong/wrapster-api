using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.TenantSettings.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Repositories;
using TenantSettingsEntity = Wrapsfer.Domain.Entities.TenantSettings;

namespace Wrapsfer.Application.TenantSettings.Queries.GetTenantSettings;

internal sealed class GetTenantSettingsQueryHandler(
    ITenantSettingsRepository tenantSettingsRepository,
    ITenantContext tenantContext) : IQueryHandler<GetTenantSettingsQuery, TenantSettingsResponse>
{
    public async Task<Result<TenantSettingsResponse>> Handle(GetTenantSettingsQuery request,
        CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        TenantSettingsEntity? settings =
            await tenantSettingsRepository.GetByTenantIdAsync(tenantId, cancellationToken);

        if (settings is null)
        {
            return new TenantSettingsResponse(tenantId, null, []);
        }

        List<NotificationRecipientResponse> recipients = settings.Recipients
            .Select(r => new NotificationRecipientResponse(r.Id, r.Email, r.IsActive))
            .ToList();

        return new TenantSettingsResponse(settings.TenantId, settings.DefaultLowStockThreshold, recipients);
    }
}
