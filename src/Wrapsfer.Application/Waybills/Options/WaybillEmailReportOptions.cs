namespace Wrapsfer.Application.Waybills.Options;

public sealed class WaybillEmailReportOptions
{
    public const string SectionName = "WaybillExport";

    public int AttachmentRowThreshold { get; init; } = 10_000;

    public long AttachmentMaxBytes { get; init; } = 25L * 1024 * 1024;
}
