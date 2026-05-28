namespace Wrapsfer.Application.Waybills.Queries.DownloadWaybillsExportFile;

public sealed record DownloadWaybillsExportFileResult(
    byte[] Content,
    string ContentType,
    string FileName);
