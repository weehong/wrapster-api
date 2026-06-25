namespace Wrapsfer.Application.Abstractions.Auditing;

/// <summary>
/// Persistence seam for application-level audit records. Implementations must write durably and
/// in isolation from the originating request's unit of work, so that auditing a failed request does
/// not flush the request's partial, unsaved changes.
/// </summary>
public interface IAuditSink
{
    Task WriteAsync(AuditLogRecord record, CancellationToken cancellationToken);
}
