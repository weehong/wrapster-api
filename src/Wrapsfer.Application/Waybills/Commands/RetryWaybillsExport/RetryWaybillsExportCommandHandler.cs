using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Queue;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;
using Wrapsfer.Application.Waybills.Common;
using Wrapsfer.Application.Waybills.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Commands.RetryWaybillsExport;

internal sealed class RetryWaybillsExportCommandHandler(
    IMessagePublisher messagePublisher,
    ITenantContext tenantContext,
    IWaybillExportJobRepository jobRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<RetryWaybillsExportCommand>
{
    public async Task<Result> Handle(RetryWaybillsExportCommand request, CancellationToken cancellationToken)
    {
        WaybillExportJob? job = await jobRepository.GetByIdAsync(request.JobId, cancellationToken);
        if (job is null)
        {
            return Result.Failure(WaybillExportJobErrors.NotFound);
        }

        if (!string.Equals(job.RequestedByUserId, tenantContext.UserId, StringComparison.Ordinal))
        {
            return Result.Failure(WaybillExportJobErrors.NotOwned);
        }

        Result retryResult = job.MarkRetrying();
        if (retryResult.IsFailure)
        {
            return retryResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        IReadOnlyList<string> tenantIds = WaybillExportJobSerializer.DeserializeIds(job.PartnerTenantIdsJson);

        WaybillExportFormat format = Enum.Parse<WaybillExportFormat>(job.Format, ignoreCase: true);

        // Status/Search filters were not persisted on the original job (see WaybillExportJobConfiguration),
        // so a retry runs without them. The persisted partner tenants, date range, and format are reused.
        WaybillsExportRequestedMessage message = new(
            job.Id,
            tenantIds,
            tenantContext.UserId,
            format,
            DateTime.UtcNow,
            job.FromDate,
            job.ToDate,
            Status: null,
            Search: null);

        await messagePublisher.PublishAsync(WaybillsExportRequestedMessage.QueueName, message, cancellationToken);

        return Result.Success();
    }
}
