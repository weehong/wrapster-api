using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Queue;
using Wrapsfer.Application.Waybills.Messaging;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;

internal sealed class RequestWaybillsExportCommandHandler(
    IMessagePublisher messagePublisher,
    IPartnerTenantRepository partnerTenantRepository,
    ITenantContext tenantContext) : ICommandHandler<RequestWaybillsExportCommand>
{
    public async Task<Result> Handle(RequestWaybillsExportCommand request, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> tenantIds;

        if (request.PartnerTenantIds is { Count: > 0 } requestedPartnerIds)
        {
            // Targeting specific partners is an owner-only, cross-tenant capability.
            if (!request.IncludeAllPartnerTenants)
            {
                return Result.Failure(WaybillExportErrors.PartnerScopeNotAllowed);
            }

            IReadOnlyList<PartnerTenant> activePartners =
                await partnerTenantRepository.ListAsync(isActive: true, cancellationToken);
            HashSet<string> activeIds = activePartners
                .Select(p => p.TenantId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            List<string> requested = requestedPartnerIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (requested.Count == 0 || requested.Any(id => !activeIds.Contains(id)))
            {
                return Result.Failure(WaybillExportErrors.UnknownOrInactivePartner);
            }

            tenantIds = requested;
        }
        else if (request.IncludeAllPartnerTenants)
        {
            IReadOnlyList<PartnerTenant> activePartners =
                await partnerTenantRepository.ListAsync(isActive: true, cancellationToken);
            tenantIds = activePartners.Select(p => p.TenantId).ToList();
        }
        else
        {
            tenantIds = [tenantContext.TenantId];
        }

        WaybillsExportRequestedMessage message = new(
            tenantIds,
            tenantContext.UserId,
            request.Format,
            DateTime.UtcNow,
            request.From,
            request.To,
            request.Status,
            request.Search);

        await messagePublisher.PublishAsync(WaybillsExportRequestedMessage.QueueName, message, cancellationToken);

        return Result.Success();
    }
}
