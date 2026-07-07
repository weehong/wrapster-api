using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Tests.Entities;

public class WaybillExportJobTests
{
    private static WaybillExportJob NewJob() => WaybillExportJob.Create(
        requestedByUserId: "user-1",
        requesterTenantId: "wrapsfer",
        format: "Csv",
        from: new DateOnly(2026, 5, 1),
        to: new DateOnly(2026, 5, 20),
        partnerTenantIdsJson: "[\"ophr168\"]");

    [Fact]
    public void MarkRetrying_FromFailed_TransitionsToRetryingAndClearsFailure()
    {
        WaybillExportJob job = NewJob();
        job.MarkFailed("boom");

        Result result = job.MarkRetrying();

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(WaybillExportJobStatus.Retrying);
        job.FailureReason.Should().BeNull();
        job.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void MarkRetrying_FromFailed_ClearsPreviousObjectKeys()
    {
        WaybillExportJob job = NewJob();
        job.RecordObjectKeys("[\"waybills/x/2026-05-01/file.csv\"]");
        job.MarkFailed("boom");

        Result result = job.MarkRetrying();

        result.IsSuccess.Should().BeTrue();
        job.ReportObjectKeysJson.Should().Be("[]");
    }

    [Fact]
    public void RecordObjectKeys_OverwritesPreviousValue()
    {
        WaybillExportJob job = NewJob();
        job.ReportObjectKeysJson.Should().Be("[]");

        job.RecordObjectKeys("[\"k1\"]");
        job.ReportObjectKeysJson.Should().Be("[\"k1\"]");

        job.RecordObjectKeys("[\"k2\",\"k3\"]");
        job.ReportObjectKeysJson.Should().Be("[\"k2\",\"k3\"]");
    }

    [Theory]
    [InlineData(WaybillExportJobStatus.Queued)]
    [InlineData(WaybillExportJobStatus.Processing)]
    [InlineData(WaybillExportJobStatus.Completed)]
    [InlineData(WaybillExportJobStatus.Retrying)]
    public void MarkRetrying_FromNonFailedStatus_ReturnsFailure(WaybillExportJobStatus status)
    {
        WaybillExportJob job = NewJob();
        switch (status)
        {
            case WaybillExportJobStatus.Processing:
                job.MarkProcessing();
                break;
            case WaybillExportJobStatus.Completed:
                job.MarkCompleted();
                break;
            case WaybillExportJobStatus.Retrying:
                job.MarkFailed("first");
                job.MarkRetrying();
                break;
        }

        Result result = job.MarkRetrying();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WaybillExportJobErrors.CannotRetryNonFailed);
    }
}
