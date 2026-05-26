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
}
