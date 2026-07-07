namespace Wrapsfer.Application.Abstractions.FileProcessing;

public sealed record WaybillReport(
    WaybillReportSummary Summary,
    IReadOnlyList<WaybillDailySummary> DailySummaries,
    IReadOnlyList<WaybillProductQuantity> ProductQuantities,
    IReadOnlyList<WaybillDetailRow> Details);
