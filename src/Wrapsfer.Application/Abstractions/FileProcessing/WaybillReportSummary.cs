namespace Wrapsfer.Application.Abstractions.FileProcessing;

public sealed record WaybillReportSummary(
    string ReportPeriod,
    int TotalWaybillRecords,
    int TotalItemsScanned,
    int UniqueProducts,
    string ExportedBy,
    DateTime GeneratedAtUtc);
