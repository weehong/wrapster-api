namespace Wrapsfer.Application.Behaviors;

/// <summary>
/// Disposable handle representing a single request's audit-metadata collection. Holds a reference to
/// the dictionary handlers write into via <see cref="AuditMetadataAccumulator"/>, and restores the
/// previous ambient collection on dispose so nested MediatR sends stay isolated.
/// </summary>
internal sealed class AuditMetadataScope(
    IReadOnlyDictionary<string, object?> values,
    Action onDispose) : IDisposable
{
    private readonly Action _onDispose = onDispose;
    private bool _disposed;

    public IReadOnlyDictionary<string, object?> Values { get; } = values;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _onDispose();
    }
}
