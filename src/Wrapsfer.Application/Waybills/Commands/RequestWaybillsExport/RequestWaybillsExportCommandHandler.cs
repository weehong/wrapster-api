using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Queue;
using Wrapsfer.Application.Waybills.Common;
using Wrapsfer.Application.Waybills.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;

internal sealed class RequestWaybillsExportCommandHandler(
    IMessagePublisher messagePublisher,
    IPartnerTenantRepository partnerTenantRepository,
    ITenantContext tenantContext,
    IWaybillExportJobRepository jobRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<RequestWaybillsExportCommand>
{
    public async Task<Result> Handle(RequestWaybillsExportCommand request, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> tenantIds;

        if (request.PartnerTenantIds is { Count: > 0 } requestedPartnerIds)
        {
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

        string? recipientEmailsJson = request.RecipientEmails is { Count: > 0 } emails
            ? WaybillExportJobSerializer.SerializeIds(emails)
            : null;

        WaybillExportJob job = WaybillExportJob.Create(
            tenantContext.UserId,
            tenantContext.TenantId,
            request.Format.ToString(),
            request.From,
            request.To,
            WaybillExportJobSerializer.SerializeIds(tenantIds),
            recipientEmailsJson);

        jobRepository.Add(job);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        WaybillsExportRequestedMessage message = new(
            job.Id,
            tenantIds,
            tenantContext.UserId,
            request.Format,
            DateTime.UtcNow,
            request.From,
            request.To,
            request.Status,
            request.Search,
            request.RecipientEmails);

        await messagePublisher.PublishAsync(WaybillsExportRequestedMessage.QueueName, message, cancellationToken);

        return Result.Success();
    }
}
