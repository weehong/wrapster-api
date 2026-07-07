namespace Wrapsfer.Domain.Repositories;

/// <summary>
/// A single successful stock-report download, projected from the application audit trail for usage
/// reporting. Only rows with <c>Action = Executed</c> and <c>Changes.successfulDownload = true</c>
/// are represented.
/// </summary>
public sealed record StockReportDownloadAudit(
    DateTime TimestampUtc,
    string? UserId,
    string? Username);
