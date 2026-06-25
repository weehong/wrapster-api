namespace Wrapsfer.Application.Abstractions.Auditing;

/// <summary>
/// Handler-facing seam for attaching structured metadata to the audit record produced for the
/// currently executing MediatR request. Values are merged into the audit log <c>Changes</c> payload
/// by <see cref="Wrapsfer.Application.Behaviors.AuditBehavior{TRequest,TResponse}"/>.
/// </summary>
public interface IAuditMetadata
{
    void Set(string key, object? value);
}
