namespace Wrapsfer.Application.Abstractions.Storage;

/// <summary>
/// Describes a report that has been persisted to object storage along with a time-limited
/// presigned download link. <see cref="ObjectKey"/> is the fully-qualified key as stored
/// (including any storage-level prefix), not the relative key supplied by the caller.
/// </summary>
public sealed record StoredReport(
    string BucketName,
    string ObjectKey,
    string DownloadUrl,
    DateTime ExpiresAtUtc);
