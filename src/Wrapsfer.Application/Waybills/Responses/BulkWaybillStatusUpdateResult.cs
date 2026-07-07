namespace Wrapsfer.Application.Waybills.Responses;

public sealed record BulkWaybillStatusUpdateResult(
    int TotalRequested,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<BulkWaybillStatusUpdateItemResult> Results);
