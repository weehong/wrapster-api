namespace Wrapsfer.Application.Abstractions.FileProcessing;

public sealed record WaybillReportMetadata(
    DateOnly? From,
    DateOnly? To,
    string? ExportedBy,
    DateTime GeneratedAtUtc);
