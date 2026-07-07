namespace Wrapsfer.Application.Abstractions.FileProcessing;

public sealed record WaybillDailySummary(
    DateOnly Date,
    int WaybillRecords,
    int ItemsScanned);
