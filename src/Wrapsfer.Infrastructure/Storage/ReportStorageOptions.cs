namespace Wrapsfer.Infrastructure.Storage;

public sealed class ReportStorageOptions
{
    public const string SectionName = "ReportStorage:S3";

    public string BucketName { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string Prefix { get; init; } = "reports";
    public int DownloadUrlTtlMinutes { get; init; } = 4320;
}
