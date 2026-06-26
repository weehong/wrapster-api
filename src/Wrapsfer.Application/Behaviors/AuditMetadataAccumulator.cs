using System.Collections.Concurrent;
using Wrapsfer.Application.Abstractions.Auditing;

namespace Wrapsfer.Application.Behaviors;

/// <summary>
/// Singleton collector that bridges handlers (which call <see cref="Set"/>) and
/// <see cref="AuditBehavior{TRequest,TResponse}"/> (which opens a scope and reads what was set).
/// The active collection is held in an <see cref="AsyncLocal{T}"/> so concurrent requests and nested
/// MediatR sends each write into their own isolated bag. The bag itself is a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> because a single request may fan out work onto
/// parallel tasks that share the captured <see cref="AsyncLocal{T}"/> value and call <see cref="Set"/>
/// concurrently.
/// </summary>
internal sealed class AuditMetadataAccumulator : IAuditMetadata
{
    private static readonly AsyncLocal<ConcurrentDictionary<string, object?>?> s_current = new();

    public void Set(string key, object? value)
    {
        ConcurrentDictionary<string, object?>? current = s_current.Value;
        if (current is not null)
        {
            current[key] = value;
        }
    }

    public AuditMetadataScope BeginScope()
    {
        ConcurrentDictionary<string, object?>? previous = s_current.Value;
        ConcurrentDictionary<string, object?> values = new();
        s_current.Value = values;

        return new AuditMetadataScope(values, () => s_current.Value = previous);
    }
}
