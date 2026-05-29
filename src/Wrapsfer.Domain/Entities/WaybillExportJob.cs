using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Entities;

public sealed class WaybillExportJob : AuditableEntity
{
    private WaybillExportJob()
    {
    }

    public string RequestedByUserId { get; private set; } = default!;
    public string RequesterTenantId { get; private set; } = default!;
    public string Format { get; private set; } = default!;
    public WaybillExportJobStatus Status { get; private set; }
    public DateOnly? FromDate { get; private set; }
    public DateOnly? ToDate { get; private set; }
    public string PartnerTenantIdsJson { get; private set; } = "[]";
    public string ReportObjectKeysJson { get; private set; } = "[]";
    public DateTime? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }

    public static WaybillExportJob Create(
        string requestedByUserId,
        string requesterTenantId,
        string format,
        DateOnly? from,
        DateOnly? to,
        string partnerTenantIdsJson) =>
        new()
        {
            RequestedByUserId = requestedByUserId,
            RequesterTenantId = requesterTenantId,
            Format = format,
            Status = WaybillExportJobStatus.Queued,
            FromDate = from,
            ToDate = to,
            PartnerTenantIdsJson = partnerTenantIdsJson
        };

    public void MarkProcessing() => Status = WaybillExportJobStatus.Processing;

    public void MarkCompleted()
    {
        Status = WaybillExportJobStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        FailureReason = null;
    }

    public void MarkFailed(string reason)
    {
        Status = WaybillExportJobStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        FailureReason = reason.Length > 512 ? reason[..512] : reason;
    }

    public Result MarkRetrying()
    {
        if (Status != WaybillExportJobStatus.Failed)
        {
            return Result.Failure(WaybillExportJobErrors.CannotRetryNonFailed);
        }

        Status = WaybillExportJobStatus.Retrying;
        FailureReason = null;
        CompletedAt = null;
        ReportObjectKeysJson = "[]";
        return Result.Success();
    }

    public void RecordObjectKeys(string objectKeysJson) =>
        ReportObjectKeysJson = objectKeysJson;
}
