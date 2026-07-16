namespace Wrapsfer.Application.Abstractions.Storage;

/// <summary>
/// Persists generated report files to private object storage and issues a time-limited
/// presigned download link for them.
/// </summary>
public interface IReportStorage
{
    /// <summary>
    /// Uploads <paramref name="content"/> as a private object and returns a presigned download link.
    /// </summary>
    /// <param name="objectKey">
    /// The report-relative key (for example <c>waybills/{tenantId}/{yyyy}/{MM}/{dd}/{exportId}/{fileName}</c>).
    /// The storage implementation prepends any configured prefix to form the final object key.
    /// </param>
    /// <param name="content">The file bytes to store.</param>
    /// <param name="contentType">The MIME content type recorded as object metadata.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<StoredReport> UploadAsync(
        string objectKey,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the object identified by <paramref name="objectKey"/>. Missing objects are treated
    /// as success — callers can replay this operation without checking existence first.
    /// </summary>
    /// <param name="objectKey">The same report-relative key passed to <see cref="UploadAsync"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a previously uploaded object's bytes. Returns null when the object
    /// does not exist (caller re-fetches from the source and re-uploads).
    /// </summary>
    Task<byte[]?> DownloadAsync(string objectKey, CancellationToken cancellationToken = default);
}
