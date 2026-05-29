using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Storage;
using Wrapsfer.Application.Waybills.Common;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Commands.DeleteWaybillsExportJob;

internal sealed class DeleteWaybillsExportJobCommandHandler(
    ITenantContext tenantContext,
    IWaybillExportJobRepository jobRepository,
    IReportStorage reportStorage,
    IUnitOfWork unitOfWork,
    ILogger<DeleteWaybillsExportJobCommandHandler> logger)
    : ICommandHandler<DeleteWaybillsExportJobCommand>
{
    public async Task<Result> Handle(
        DeleteWaybillsExportJobCommand request, CancellationToken cancellationToken)
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

        // Restrict to terminal states so we don't race with an in-flight consumer that's about to
        // write to MarkProcessing/MarkCompleted/MarkFailed and resurrect a row we just deleted.
        if (job.Status != WaybillExportJobStatus.Completed
            && job.Status != WaybillExportJobStatus.Failed)
        {
            return Result.Failure(WaybillExportJobErrors.CannotDeleteNonTerminal);
        }

        IReadOnlyList<string> objectKeys =
            WaybillExportJobSerializer.DeserializeIds(job.ReportObjectKeysJson);

        foreach (string objectKey in objectKeys)
        {
            try
            {
                await reportStorage.DeleteAsync(objectKey, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log and keep going — a missing object isn't worth blocking the row delete, and
                // partial failure leaves orphan files that a bucket lifecycle policy can sweep.
                logger.LogWarning(ex,
                    "Failed to delete report object {ObjectKey} for export job {JobId}",
                    objectKey, job.Id);
            }
        }

        jobRepository.Remove(job);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
